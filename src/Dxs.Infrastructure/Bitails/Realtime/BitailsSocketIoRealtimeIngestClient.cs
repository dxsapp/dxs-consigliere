using System;
using System.Collections.Generic;
using System.Reactive.Subjects;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using SocketIOClient.Transport;

namespace Dxs.Infrastructure.Bitails.Realtime;

public sealed class BitailsSocketIoRealtimeIngestClient(ILogger<BitailsSocketIoRealtimeIngestClient> logger) : IBitailsRealtimeIngestClient
{
    public async Task<IBitailsRealtimeConnection> ConnectAsync(
        BitailsRealtimeTransportPlan plan,
        string apiKey = null,
        CancellationToken cancellationToken = default
    )
    {
        if (plan is null)
            throw new ArgumentNullException(nameof(plan));

        if (plan.Mode != BitailsRealtimeTransportMode.WebSocket)
            throw new NotSupportedException($"Bitails realtime transport `{plan.Mode}` is not supported by the websocket ingest client.");

        var options = new SocketIOClient.SocketIOOptions
        {
            Transport = TransportProtocol.WebSocket,
            Reconnection = true,
            ReconnectionAttempts = int.MaxValue,
            ReconnectionDelayMax = 5_000,
            ConnectionTimeout = TimeSpan.FromSeconds(15)
        };

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            options.ExtraHeaders = new Dictionary<string, string>
            {
                ["x-api-key"] = apiKey
            };
        }

        var socket = new SocketIOClient.SocketIO(plan.Endpoint, options);
        var connection = new BitailsSocketIoRealtimeConnection(socket, plan, logger);

        await connection.ConnectAsync(cancellationToken);
        return connection;
    }

    private sealed class BitailsSocketIoRealtimeConnection(
        SocketIOClient.SocketIO socket,
        BitailsRealtimeTransportPlan plan,
        ILogger logger
    ) : IBitailsRealtimeConnection
    {
        private readonly Subject<BitailsRealtimeEvent> _events = new();
        private readonly List<string> _topics = [.. plan.Topics];

        public IObservable<BitailsRealtimeEvent> Events => _events;

        public async Task ConnectAsync(CancellationToken cancellationToken)
        {
            foreach (var topic in _topics)
            {
                socket.On(topic, response =>
                {
                    TryPublish(topic, response);
                    return Task.CompletedTask;
                });
            }

            socket.OnConnected += (_, _) =>
            {
                logger.LogDebug("Bitails realtime socket connected to {Endpoint} with {TopicCount} topics", plan.Endpoint, _topics.Count);
            };
            socket.OnDisconnected += (_, reason) =>
            {
                logger.LogWarning("Bitails realtime socket disconnected from {Endpoint}: {Reason}", plan.Endpoint, reason);
            };
            socket.OnReconnectAttempt += (_, attempt) =>
            {
                logger.LogDebug("Bitails realtime socket reconnect attempt {Attempt}", attempt);
            };
            socket.OnError += (_, error) =>
            {
                logger.LogError("Bitails realtime socket error from {Endpoint}: {Error}", plan.Endpoint, error);
            };

            await socket.ConnectAsync(cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                if (socket.Connected)
                    await socket.DisconnectAsync();
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Bitails realtime socket disconnect failed");
            }
            finally
            {
                _events.OnCompleted();
                _events.Dispose();
                socket.Dispose();
            }
        }

        private void TryPublish(string topic, SocketIOClient.SocketIOResponse response)
        {
            try
            {
                if (!TryExtractTransactionId(response, out var txId))
                {
                    logger.LogDebug("Bitails realtime topic {Topic} did not expose a txid in payload {Payload}", topic, response.ToString());
                    return;
                }

                _events.OnNext(new BitailsRealtimeEvent(BitailsRealtimeEventKind.TransactionAdded, topic, DateTimeOffset.UtcNow, txId));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to parse Bitails realtime payload for topic {Topic}", topic);
            }
        }

        private static bool TryExtractTransactionId(SocketIOClient.SocketIOResponse response, out string txId)
        {
            txId = null;

            try
            {
                var direct = response.GetValue<string>(0);
                if (BitailsRealtimePayloadParser.TryExtractTxId(direct, out txId))
                    return true;
            }
            catch
            {
            }

            try
            {
                var payload = response.GetValue<JsonElement>(0);
                if (BitailsRealtimePayloadParser.TryExtractTxId(payload, out txId))
                    return true;
            }
            catch
            {
            }

            return false;
        }
    }
}
