using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Dxs.Bsv.BitcoinMonitor;
using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Bsv.Models;
using Dxs.Bsv.Zmq.Configs;
using Dxs.Bsv.Zmq.Dto;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NetMQ;
using NetMQ.Sockets;

namespace Dxs.Bsv.Zmq;

public class ZmqClient : IZmqClient
{
    private readonly ITxMessageBus _txMessageBus;
    private readonly IBlockMessageBus _blockMessageBus;
    private readonly ILogger _logger;
    private readonly Dictionary<string, string> _addressByTopic = new();

    private const string RawTx2Topic = "rawtx2";
    private const string RemovedFromMempoolBlockTopic = "removedfrommempoolblock";
    private const string DiscardedFromMempoolTopic = "discardedfrommempool";
    private const string HashBlock2Topic = "hashblock2";

    private static readonly TimeSpan ReceiveTimeout = TimeSpan.FromSeconds(5);

    public ZmqClient(
        IOptions<ZmqClientConfig> configuration,
        ITxMessageBus txMessageBus,
        IBlockMessageBus blockMessageBus,
        ILogger<ZmqClient> logger
    )
    {
        _txMessageBus = txMessageBus;
        _blockMessageBus = blockMessageBus;
        _logger = logger;

        _addressByTopic[RawTx2Topic] = configuration.Value.RawTx2Address;
        _addressByTopic[RemovedFromMempoolBlockTopic] = configuration.Value.RemovedFromMempoolBlockAddress;
        _addressByTopic[DiscardedFromMempoolTopic] = configuration.Value.DiscardedFromMempoolAddress;
        _addressByTopic[HashBlock2Topic] = configuration.Value.HashBlock2Address;
    }

    public Task Start(CancellationToken cancellationToken)
    {
        Task.Run(() => SubscribeToRawTx(cancellationToken), cancellationToken);
        Task.Run(() => SubscribeToRemovedFromMempoolBlock(cancellationToken), cancellationToken);
        Task.Run(() => SubscribeToDiscardedFromMempool(cancellationToken), cancellationToken);
        Task.Run(() => SubscribeToNewBlockConnected(cancellationToken), cancellationToken);

        return Task.CompletedTask;
    }

    private void SubscribeToRawTx(CancellationToken cancellationToken)
    {
        SubscribeToTopic(RawTx2Topic, cancellationToken, payload =>
        {
            try
            {
                var tx = Transaction.Parse(payload, Network.Mainnet);

                _txMessageBus.Post(TxMessage.AddedToMempool(tx, DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Unable to parse transaction: {Length}:{Bytes}",
                    payload.Length,
                    payload.ToHexString()
                );
            }
        });
    }

    private void SubscribeToRemovedFromMempoolBlock(CancellationToken cancellationToken)
    {
        SubscribeToTopic(RemovedFromMempoolBlockTopic, cancellationToken, payload =>
        {
            try
            {
                var message = JsonSerializer.Deserialize<RemovedFromMempoolBlockMessage>(payload);
                var txMessage = TxMessage.RemovedFromMempool(message.TxId, Map(message.Reason));

                _txMessageBus.Post(txMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to deserialize message: {Message}", payload.ToHexString());
            }
        });
    }

    private void SubscribeToDiscardedFromMempool(CancellationToken cancellationToken)
    {
        SubscribeToTopic(DiscardedFromMempoolTopic, cancellationToken, payload =>
        {
            try
            {
                var message = JsonSerializer.Deserialize<DiscardedFromMempoolMessage>(payload);
                var txMessage = TxMessage.RemovedFromMempool(
                    message.TxId,
                    Map(message.Reason),
                    message.CollidedWith.TxId,
                    message.BlockHash
                );

                _txMessageBus.Post(txMessage);
                _logger.LogDebug(
                    "Discarded from Mempool {Reason}:{TxId}; {CollidedWith}; {BlockHash}",
                    message.Reason,
                    message.TxId,
                    message.CollidedWith.TxId,
                    message.BlockHash
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to deserialize message: {Message}", payload.ToHexString());
            }
        });
    }

    private void SubscribeToNewBlockConnected(CancellationToken cancellationToken)
    {
        SubscribeToTopic(HashBlock2Topic, cancellationToken, payload =>
        {
            try
            {
                var blockHash = payload.ToHexString();
                var blockMessage = new BlockMessage(blockHash);

                _blockMessageBus.Post(blockMessage);
                _logger.LogDebug("Block connected {Hash}", blockHash);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to deserialize message: {Message}", payload.ToHexString());
            }
        });
    }

    private void SubscribeToTopic(string topic, CancellationToken cancellationToken, Action<byte[]> handlePayload)
    {
        using var subscriber = new SubscriberSocket();

        subscriber.Connect(_addressByTopic[topic]);
        subscriber.Subscribe(topic);

        var topicBytes = Encoding.UTF8.GetBytes(topic);

        while (!cancellationToken.IsCancellationRequested)
        {
            var msg = new NetMQMessage();

            if (!subscriber.TryReceiveMultipartMessage(ReceiveTimeout, ref msg, 3))
                continue;

            var payload = ReadMessageOfTopic(msg, topicBytes);

            if (payload != null)
            {
                handlePayload(payload);
            }
        }
    }

    private byte[] ReadMessageOfTopic(NetMQMessage msg, byte[] expectedTopic)
    {
        if (msg.Count() != 3)
        {
            _logger.LogError("Unexpected message: {Topic}", Encoding.UTF8.GetString(msg.First.Buffer));
            return null;
        }

        var topic = msg[0];
        var body = msg[1];

        if (!topic.Buffer.SequenceEqual(expectedTopic))
        {
            _logger.LogDebug("Unknown topic: {Topic}", Encoding.UTF8.GetString(msg.First.Buffer));

            return null;
        }

        return body.Buffer;
    }

    private RemoveFromMempoolReason Map(string reason)
        => reason switch
        {
            "expired" => RemoveFromMempoolReason.Expired,
            "mempool-sizelimit-exceeded" => RemoveFromMempoolReason.MempoolSizeLimitExceeded,
            "collision-in-block-tx" => RemoveFromMempoolReason.CollisionInBlockTx,
            "reorg" => RemoveFromMempoolReason.Reorg,
            "included-in-block" => RemoveFromMempoolReason.IncludedInBlock,
            _ => RemoveFromMempoolReason.Unknown
        };
}
