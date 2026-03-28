using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.WebSockets;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

using Dxs.Common.Extensions;
using Dxs.Infrastructure.Common;
using Dxs.Infrastructure.JungleBus.Dto;
using Dxs.Infrastructure.Websocket;

using Microsoft.Extensions.Logging;

using TrustMargin.Common.Extensions;

using Websocket.Client;

namespace Dxs.Infrastructure.JungleBus;

public class JungleBusWebsocketClient(
    HttpClient httpClient,
    IExternalChainProviderSettingsAccessor providerSettingsAccessor,
    ILogger<JungleBusWebsocketClient> logger
) : BaseWebsocketClient<string>(logger)
{
    private static readonly Regex IdOnlyRegex = new("""^{"id":\s*\d+}$""", RegexOptions.Compiled);

    private readonly Subject<PubTransactionDto> _blockStream = new();
    private readonly Subject<PubTransactionDto> _mempoolStream = new();
    private readonly Subject<PubControlMessageDto> _controlMessagesStream = new();

    private int _id;
    private string _subscription;
    private Uri _socketUrl = new("wss://junglebus.gorillapool.io/connection/websocket");
    private Uri _tokenUrl = new("https://junglebus.gorillapool.io/v1/user/subscription-token");
    private string _apiKey;
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
        var settings = await providerSettingsAccessor.GetJungleBusAsync();
        (_socketUrl, _tokenUrl) = BuildEndpoints(settings.BaseUrl);
        _apiKey = settings.ApiKey;

        await base.Start();

        var timer = new System.Timers.Timer(TimeSpan.FromSeconds(5));
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

    protected override void StopInternal() { }

    public override string Name => "JungleBus";
    protected override Uri Url => _socketUrl;
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

                if (data?.Push?.Channel is { } c)
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

                if (data?.Push?.Channel is { } c)
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

                if (data?.Push?.Channel is { } c)
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
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Content-Type"] = "application/json"
        };
        if (!string.IsNullOrWhiteSpace(_apiKey))
            headers["Authorization"] = _apiKey;

        var response = await httpClient.PostOrThrowAsync<TokenDto>(
            _tokenUrl.ToString(),
            new
            {
                id = _subscription
            },
            headers: headers
        );

        return response.Token;
    }

    private void Pong()
    {
        SendAsString(new { });
    }


    private void SubscribeToChannel(string channel)
    {
        SendAsString(new { id = ++_id, subscribe = new { channel } }, true);
    }

    private string SubscriptionChannel => $"query:{_subscription}";
    private string MempoolChannel => $"{SubscriptionChannel}:mempool";
    private string ControlMessageChannel => $"{SubscriptionChannel}:control";
    private string BlockChannel => $"{SubscriptionChannel}:{_height}";

    private static (Uri socketUrl, Uri tokenUrl) BuildEndpoints(string baseUrl)
    {
        var raw = string.IsNullOrWhiteSpace(baseUrl)
            ? "https://junglebus.gorillapool.io"
            : baseUrl.Trim().TrimEnd('/');

        var tokenUrl = new Uri($"{raw}/v1/user/subscription-token", UriKind.Absolute);
        var socketBase = raw.Replace("https://", "wss://", StringComparison.OrdinalIgnoreCase)
            .Replace("http://", "ws://", StringComparison.OrdinalIgnoreCase);
        var socketUrl = new Uri($"{socketBase}/connection/websocket", UriKind.Absolute);
        return (socketUrl, tokenUrl);
    }
}
