using System;
using System.Net.Http;
using System.Net.WebSockets;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Timers;
using Dxs.Common.Extensions;
using Dxs.Infrastructure.JungleBus.Dto;
using Dxs.Infrastructure.Websocket;
using Microsoft.Extensions.Logging;
using TrustMargin.Common.Extensions;
using Websocket.Client;

namespace Dxs.Infrastructure.JungleBus;

public class JungleBusWebsocketClient(
    HttpClient httpClient,
    ILogger<JungleBusWebsocketClient> logger
): BaseWebsocketClient<string>(logger)
{
    private static readonly Regex IdOnlyRegex = new("""^{"id":\s*\d+}$""", RegexOptions.Compiled);

    private readonly Subject<PubTransactionDto> _blockStream = new();
    private readonly Subject<PubTransactionDto> _mempoolStream = new();
    private readonly Subject<PubControlMessageDto> _controlMessagesStream = new();

    private int _id;
    private string _subscription;
    private int _height;
    private bool _isMempoolActive;
    private bool _isControlActive;

    public override Task Start()
    {
        if (_subscription.IsNullOrEmpty())
            throw new Exception($"Use {nameof(StartSubscription)} instead");

        return base.Start();
    }

    public async Task StartSubscription(string subscription)
    {
        _subscription = subscription;

        await base.Start();

        var timer = new Timer(TimeSpan.FromSeconds(5));
        timer.Elapsed += (_, _) => Pong();
        timer.AutoReset = true;
        timer.Enabled = true;
        timer.AddToCompositeDisposable(Subscriptions);
    }

    protected override void InitializeWebsocketClient(WebsocketClient websocketClient)
    {
        websocketClient
            .MessageReceived
            .Where(x => x.MessageType == WebSocketMessageType.Text)
            .Select(x => x.Text.Split('\n'))
            .SelectMany(x => x)
            .Select(x =>
            {
                ResetIdleTimer();

                if (!x.Contains("push")) // tooooo slow
                {
                    Pong();
                    // return null;
                }

                if (IdOnlyRegex.IsMatch(x))
                {
                    return null;
                }

                return x;
            })
            .Where(x => x != null)
            .Subscribe(TickerObservable)
            .AddToCompositeDisposable(Subscriptions);
    }

    protected override void StopInternal() {}

    public override string Name => "JungleBus";
    protected override Uri Url => new("wss://junglebus.gorillapool.io/connection/websocket");
    protected override bool IsTextMessageConversionEnabled => true;

    public void SubscribeToMempool()
    {
        if (_isMempoolActive)
            return;

        _isMempoolActive = true;

        var channel = MempoolChannel;
        TickerObservable
            .Select(x =>
            {
                var data = Deserialize<PushDto<PubTransactionDto>>(x);

                if (data?.Push?.Channel is {} c)
                {
                    if (c == channel)
                    {
                        return data!.Push!.Pub.Data;
                    }
                }
                else
                {
                    LogPayload(x.OfMaxBytes(240000));
                }

                return null;
            })
            .Where(x => x != null)
            .Subscribe(_mempoolStream)
            .AddToCompositeDisposable(Subscriptions);

        SubscribeToChannel(channel);
    }

    public void SubscribeToControlMessages()
    {
        if (_isControlActive)
            return;

        _isControlActive = true;

        var channel = ControlMessageChannel;
        TickerObservable
            .Select(x =>
            {
                var data = Deserialize<PushDto<PubControlMessageDto>>(x);

                if (data?.Push?.Channel is {} c)
                {
                    if (c == channel)
                    {
                        return data!.Push!.Pub.Data;
                    }
                }
                else
                {
                    LogPayload(x.OfMaxBytes(240000));
                }

                return null;
            })
            .Where(x => x != null)
            .Subscribe(_controlMessagesStream)
            .AddToCompositeDisposable(Subscriptions);

        SubscribeToChannel(channel);
    }

    /// <summary>
    /// Not thread safe!!
    /// </summary>
    public IDisposable CrawlBlock(int height)
    {
        _height = height;

        var channel = BlockChannel;
        var sub = TickerObservable
            .Select(x =>
            {
                var data = Deserialize<PushDto<PubTransactionDto>>(x);

                if (data?.Push?.Channel is {} c)
                {
                    if (c == channel)
                    {
                        return data!.Push!.Pub.Data;
                    }
                }
                else
                {
                    LogPayload(x.OfMaxBytes(240000));
                }

                return null;
            })
            .Where(x => x != null)
            .Subscribe(_blockStream)
            .AddToCompositeDisposable(Subscriptions);

        SubscribeToChannel(channel);

        return sub;
    }

    public IObservable<PubTransactionDto> Mempool => _mempoolStream.AsObservable();
    public IObservable<PubTransactionDto> Block => _blockStream.AsObservable();
    public IObservable<PubControlMessageDto> ControlMessages => _controlMessagesStream.AsObservable();

    public void Pause() => SendAsString(new { cmd = "pause" });
    public void Unpause() => SendAsString(new { cmd = "start" });

    protected override async Task<string[]> RestoreState()
    {
        var token = await GetToken();
        SendAsString(new { id = ++_id, connect = new { token } });

        if (_isMempoolActive)
            SubscribeToChannel(MempoolChannel);

        if (_isControlActive)
            SubscribeToChannel(ControlMessageChannel);

        if (_height != 0)
            SubscribeToChannel(BlockChannel);

        return [];
    }

    protected override Task ResetState() => Task.CompletedTask;

    private async Task<string> GetToken()
    {
        var response = await httpClient.PostOrThrowAsync<TokenDto>(
            "https://junglebus.gorillapool.io/v1/user/subscription-token",
            new
            {
                id = _subscription
            },
            headers: new
            {
                ContentType = "application/json"
            }
        );

        return response.Token;
    }

    private void Pong()
    {
        SendAsString(new {});
    }


    private void SubscribeToChannel(string channel)
    {
        SendAsString(new { id = ++_id, subscribe = new { channel } }, true);
    }

    private string SubscriptionChannel => $"query:{_subscription}";
    private string MempoolChannel => $"{SubscriptionChannel}:mempool";
    private string ControlMessageChannel => $"{SubscriptionChannel}:control";
    private string BlockChannel => $"{SubscriptionChannel}:{_height}";
}