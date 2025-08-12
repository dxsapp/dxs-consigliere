using System.Runtime.Serialization;
using System.Threading.Tasks.Dataflow;
using Dxs.Bsv;
using Dxs.Bsv.BitcoinMonitor;
using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Bsv.Models;
using Dxs.Common.BackgroundTasks;
using Dxs.Common.Time;
using Dxs.Consigliere.Configs;
using Dxs.Infrastructure.Bitails;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SocketIOClient;
using SocketIOClient.Transport;
using SpanJson;

namespace Dxs.Consigliere.BackgroundTasks;

public class BitailsMempoolMonitor(
    ITxMessageBus messageBus,
    IBitailsRestApiClient bitailsRestApiClient,
    IOptions<AppConfig> appConfig,
    ILogger<BitailsMempoolMonitor> logger
) : PeriodicTask(appConfig.Value.BackgroundTasks, logger)
{
    public class SlimResponse
    {
        [DataMember(Name = "txid")]
        public string TxId { get; set; }
    }

    public class Input
    {
        [DataMember(Name = "source")]
        public Output Source { get; set; }
    }

    public class Output
    {
        [DataMember(Name = "scripthash")]
        public string ScriptHash { get; set; }
    }

    public class MempoolSlimResponse
    {
        [DataMember(Name = "txid")]
        public string TxId { get; set; }

        [DataMember(Name = "inputs")]
        public Input[] Inputs { get; set; }

        [DataMember(Name = "outputs")]
        public Output[] Outputs { get; set; }
    }

    private readonly AppConfig _appConfig = appConfig.Value;
    private readonly ILogger _logger = logger;

    protected override TimeSpan Period => Timeout.InfiniteTimeSpan;
    protected override TimeSpan WaitTimeOnError => TimeSpan.FromSeconds(30);
    public override string Name => nameof(BitailsMempoolMonitor);

    protected override async Task RunAsync(CancellationToken cancellationToken)
    {
        using var _ = _logger.BeginScope("{Name}", nameof(BitailsMempoolMonitor));
        using var client = new SocketIOClient.SocketIO(
            "https://api.bitails.io/global",
            new SocketIOOptions { Transport = TransportProtocol.WebSocket }
        );

        var transactionProcessor = new ActionBlock<string>(
            DownloadTransaction,
            new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = 3,
                CancellationToken = cancellationToken
            });

        client.On("tx", response =>
        {
            try
            {
                var responses = Deserialize<MempoolSlimResponse[]>(response.ToString());

                foreach (var mempoolSlimResponse in responses)
                {
                    var posted = false;

                    foreach (var input in mempoolSlimResponse.Inputs)
                    {
                        if (_appConfig.Bitails.ScriptHashes.Contains(input.Source.ScriptHash))
                        {
                            transactionProcessor.Post(mempoolSlimResponse.TxId);
                            posted = true;
                            break;
                        }
                    }

                    if (posted)
                        break;

                    foreach (var output in mempoolSlimResponse.Outputs)
                    {
                        if (_appConfig.Bitails.ScriptHashes.Contains(output.ScriptHash))
                        {
                            transactionProcessor.Post(mempoolSlimResponse.TxId);
                            break;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Bitails transaction processing error: {Response}", response);
            }
        });

        await client.ConnectAsync();

        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
        }

        transactionProcessor.Complete();
    }

    private async Task DownloadTransaction(string txId)
    {
        try
        {
            var body = await bitailsRestApiClient.GetTransactionRawOrNullAsync(txId);

            if (body == null)
                return;

            var transaction = Transaction.Parse(body, Network.Mainnet);

            _logger.LogDebug("Bitails Tx found: {Response}", transaction.Id);
            messageBus.Post(TxMessage.AddedToMempool(transaction, DateTime.UtcNow.ToUnixSeconds()));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to download transaction from Bitails: {Hash}", txId);
        }
    }

    private static T Deserialize<T>(string value)
        => JsonSerializer.Generic.Utf16.Deserialize<T>(value);
}