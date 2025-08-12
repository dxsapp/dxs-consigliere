using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dxs.Common.Extensions;
using Dxs.Common.Utils;
using Microsoft.Extensions.Logging;
using OneOf;
using SpanJson;
using TrustMargin.Common.Extensions;
using Websocket.Client;

namespace Dxs.Infrastructure.Websocket;

public abstract class BaseWebsocketClient<TTickerDto>: IWebsocketClient
{
    protected readonly ILogger Logger;
    protected readonly CompositeDisposable Subscriptions = new();
    protected readonly Subject<TTickerDto> TickerObservable = new();
    protected WebsocketClient WebsocketClient => _websocketClient;

    private WebsocketClient _websocketClient;
    private CancellationTokenSource _cts;

    // ReSharper disable once StaticMemberInGenericType
    private static readonly NamedAsyncLock Locker = new();

    protected BaseWebsocketClient(ILogger logger)
    {
        Logger = logger;
        // ReSharper disable once VirtualMemberCallInConstructor
        Logger.LogDebug("Created websocket client \"{Name}\"", Name);
    }

    public bool IsRunning => WebsocketClient is { IsRunning: true };

    public abstract string Name { get; }

    public TimeSpan ReconnectionDelay => TimeSpan.FromSeconds(20);

    /// <summary>
    /// Source will be restarted if no tickers arrive for this period.
    /// </summary>
    public TimeSpan IdleRestartTimeout => TimeSpan.FromSeconds(30);

    public DateTime LastIdleLogAt { get; set; } = DateTime.MinValue;
    public DateTime LastTickerAt { get; set; } = DateTime.UtcNow;

    public virtual async Task Start()
    {
        using var _ = await LockByName();

        Interlocked.Exchange(ref _cts, new());
        Interlocked.Exchange(ref _websocketClient, CreateWebsocketClient());

        Logger.LogDebug("Starting websocket client, state: {@State}", GetState());

        if (!EnsureReadyToStart())
            return;

        if (WebsocketClient.IsRunning)
        {
            Logger.LogDebug("Attempt to start running websocket client");
            return;
        }

        try
        {
            await WebsocketClient.StartOrFail();

            Logger.LogDebug("Websocket client {Name} started", Name);

            ResetIdleTimer();
        }
        catch (Exception e)
        {
            Logger.LogError(e, "Failed to start websocket client {Name}", Name);

            // await Restart("Failed to start, reconnecting", ReconnectionDelay);
        }
    }

    public virtual async Task Stop(string statusDescription = "Close")
    {
        using var _ = await LockByName();

        if (WebsocketClient == null)
            return;

        Logger.LogDebug("Stopping websocket client, state: {@State}", GetState());

        if (!WebsocketClient.IsRunning)
        {
            Logger.LogDebug("Attempt to stop not running websocket client");

            return;
        }

        try
        {
            StopInternal();

            _cts.Cancel();
            Subscriptions.Clear();
            WebsocketClient.TryDispose();

            // await WebsocketClient.StopOrFail(WebSocketCloseStatus.NormalClosure, statusDescription);

            Logger.LogDebug("Websocket client {Name} stopped; Status: {Status}; State: {@State}",
                Name, statusDescription, GetState());
        }
        catch (Exception e)
        {
            Logger.LogError(e, "Failed to stop websocket client {Name}; Status: {Status}; State: {@State}",
                Name, statusDescription, GetState());
        }
    }

    public async Task Restart(string statusDescription, TimeSpan delayBeforeStarting)
    {
        using var _ = Logger.BeginScope("{Name} Restart", Name);

        Logger.LogDebug(
            """Detected attempt to force reinitialize websocket client "{Name}" with {Reason} """,
            Name, statusDescription
        );

        await ResetState();
        await Task.Delay(TimeSpan.FromSeconds(5));
        await Stop(statusDescription);
        await Task.Delay(delayBeforeStarting);
        await Start();
    }

    public void LogWsStatus()
    {
        Logger.LogDebug("Checking websocket client inactivity: {@WebsocketClientStatus}", new
        {
            LastTickerAt,
            WebsocketClient.IsStarted,
            WebsocketClient.IsRunning,
            WebsocketClient.NativeClient?.State,
            WebsocketClient.NativeClient?.CloseStatus,
            WebsocketClient.NativeClient?.CloseStatusDescription
        });
    }

    public virtual void Dispose()
    {
        try
        {
            _cts.Cancel();
        }
        catch
        {
            // ignored
        }

        _cts.TryDispose();
        Subscriptions.TryDispose();
        WebsocketClient.TryDispose();
        TickerObservable.Dispose();

        Logger.LogDebug("Disposed websocket client \"{Name}\"", Name);
    }

    private Task<IDisposable> LockByName() => Locker.LockAsync(Name);

    private WebsocketClient CreateWebsocketClient()
    {
        var ws = new WebsocketClient(Url)
        {
            // ReconnectTimeout = ReconnectionTimeout,
            IsReconnectionEnabled = false,
            IsTextMessageConversionEnabled = IsTextMessageConversionEnabled,
            Name = Name
        };

        ws.DisconnectionHappened
            .Subscribe(
                x =>
                {
                    if (x.Type is DisconnectionType.ByUser) return;

                    Logger.LogError(x.Exception,
                        "Disconnection happened; Instance: {Instance}; Type: {Type}; CloseStatus: {CloseStatus}; CloseStatusDescription: {CloseStatusDescription}",
                        Name, x.Type.ToString("G"), x.CloseStatus, x.CloseStatusDescription);

                    // using var _ = Logger.BeginScope("{Name} Handle disconnection", Name);

                    // await Restart(x.CloseStatusDescription, ReconnectionDelay);
                },
                exception => Logger.LogError(exception, "Failed to restart WebsocketClient: {Name}", Name)
            ).AddToCompositeDisposable(Subscriptions);

        ws.ReconnectionHappened
            .SubscribeAsync(
                async info =>
                {
                    Logger.LogDebug("Reconnection happened, type: {Type}", info.Type);

                    await RestoreStateInternal();
                },
                exception => Logger.LogError(exception, "Failed to restore state for WebsocketClient: {Name}", Name)
            ).AddToCompositeDisposable(Subscriptions);

        ws.MessageReceived
            .Where(x => !IsTextMessageConversionEnabled ? !string.IsNullOrEmpty(x.Text) : x.Binary?.Any() ?? false)
            .Select<ResponseMessage, OneOf<string, byte[]>>(x => !IsTextMessageConversionEnabled ? x.Text : x.Binary)
            .Subscribe(LogPayload)
            .AddToCompositeDisposable(Subscriptions);

        InitializeWebsocketClient(ws);

        return ws;
    }

    protected abstract void InitializeWebsocketClient(WebsocketClient websocketClient);
    protected abstract void StopInternal();

    private async Task RestoreStateInternal()
    {
        var restoredState = await RestoreState();
        Logger.LogDebug("Restored websocket client state: {@RestoredState}", [restoredState]);
    }

    protected abstract Uri Url { get; }

    protected virtual bool IsTextMessageConversionEnabled => false;
    protected virtual bool EnsureReadyToStart() => true;

    protected void SendAsString<T>(T request, bool logSerializedRequest = false)
    {
        if (WebsocketClient?.IsRunning != true)
            return;

        var payload = Serialize(request);

        if (string.IsNullOrEmpty(payload))
        {
            Logger.LogWarning("Attempt to send empty message: {Request}", request);
            return;
        }

        if (logSerializedRequest)
        {
            Logger.LogDebug("Log request \"{Name}\" {Request}", Name, payload);
        }

        WebsocketClient.Send(payload);
    }

    protected abstract Task<string[]> RestoreState();

    protected abstract Task ResetState();

    protected void LogPayload(OneOf<string, byte[]> payload)
    {
        if (payload.IsT0)
            LogPayload(payload.AsT0);
        else if (payload.IsT1)
            LogPayload(payload.AsT1);
    }

    protected void LogPayload(string payload)
    {
        if (!string.IsNullOrEmpty(payload))
        {
            Logger.LogDebug("Unhandled payload: {Message}", payload);
        }
    }

    protected void LogPayload(byte[] payload)
    {
        if (payload != null && payload.Any())
            LogPayload(Encoding.UTF8.GetString(payload));
    }

    public void ResetIdleTimer() => LastTickerAt = DateTime.UtcNow;

    protected static string Serialize<T>(T value)
        => JsonSerializer.Generic.Utf16.Serialize(value);

    protected static T Deserialize<T>(byte[] value)
        => JsonSerializer.Generic.Utf8.Deserialize<T>(value);

    protected static T Deserialize<T>(string value)
        => JsonSerializer.Generic.Utf16.Deserialize<T>(value);

    protected bool TryDeserialize<T>(byte[] value, out T result)
    {
        try
        {
            result = Deserialize<T>(value);
            return true;
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Failed to deserialize {MessageBytes}", value.Take(100));

            result = default;
            return false;
        }
    }

    private object GetState() => new { WebsocketClient.IsRunning, WebsocketClient.IsStarted };
}