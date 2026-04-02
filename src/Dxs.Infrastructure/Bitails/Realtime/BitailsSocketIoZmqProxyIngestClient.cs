using System;
using System.Collections.Generic;
using System.Reactive.Subjects;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

namespace Dxs.Infrastructure.Bitails.Realtime;

public sealed class BitailsSocketIoZmqProxyIngestClient(ILogger<BitailsSocketIoZmqProxyIngestClient> logger) : IBitailsRealtimeIngestClient
{
    public async Task<IBitailsRealtimeConnection> ConnectAsync(
        BitailsRealtimeTransportPlan plan,
        string apiKey = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(plan);

        if (plan.Mode != BitailsRealtimeTransportMode.Zmq)
            throw new NotSupportedException($"Bitails realtime transport `{plan.Mode}` is not supported by the ZMQ proxy ingest client.");

        var options = new SocketIOClient.SocketIOOptions
        {
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
        var connection = new BitailsSocketIoZmqProxyConnection(socket, plan, logger);

        await connection.ConnectAsync(cancellationToken);
        return connection;
    }

    private sealed class BitailsSocketIoZmqProxyConnection(
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
                logger.LogDebug("Bitails ZMQ proxy socket connected to {Endpoint} with {TopicCount} topics", plan.Endpoint, _topics.Count);
            };
            socket.OnDisconnected += (_, reason) =>
            {
                logger.LogWarning("Bitails ZMQ proxy socket disconnected from {Endpoint}: {Reason}", plan.Endpoint, reason);
            };
            socket.OnReconnectAttempt += (_, attempt) =>
            {
                logger.LogDebug("Bitails ZMQ proxy socket reconnect attempt {Attempt}", attempt);
            };
            socket.OnError += (_, error) =>
            {
                logger.LogError("Bitails ZMQ proxy socket error from {Endpoint}: {Error}", plan.Endpoint, error);
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
                logger.LogDebug(ex, "Bitails ZMQ proxy socket disconnect failed");
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
                if (TryCreateEvent(topic, response, out var realtimeEvent))
                {
                    _events.OnNext(realtimeEvent);
                    return;
                }

                logger.LogDebug("Bitails ZMQ proxy topic {Topic} emitted an unsupported payload {Payload}", topic, response.ToString());
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to parse Bitails ZMQ proxy payload for topic {Topic}", topic);
            }
        }

        private static bool TryCreateEvent(string topic, SocketIOClient.SocketIOResponse response, out BitailsRealtimeEvent realtimeEvent)
        {
            realtimeEvent = null;

            if (TryCreateEventFromString(topic, response, out realtimeEvent))
                return true;

            if (TryCreateEventFromJson(topic, response, out realtimeEvent))
                return true;

            return false;
        }

        private static bool TryCreateEventFromString(
            string topic,
            SocketIOClient.SocketIOResponse response,
            out BitailsRealtimeEvent realtimeEvent)
        {
            realtimeEvent = null;

            string payload;
            try
            {
                payload = response.GetValue<string>(0);
            }
            catch
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(payload))
                return false;

            var observedAt = DateTimeOffset.UtcNow;
            switch (topic)
            {
                case BitailsRealtimeTopicCatalog.ZmqRawTx2Topic:
                    if (!BitailsRealtimePayloadParser.TryExtractRawHex(payload, out var rawHex))
                        return false;

                    realtimeEvent = new BitailsRealtimeEvent(
                        BitailsRealtimeEventKind.TransactionAdded,
                        topic,
                        observedAt,
                        RawTransaction: Convert.FromHexString(rawHex));
                    return true;
                case BitailsRealtimeTopicCatalog.ZmqHashBlock2Topic:
                    if (!BitailsRealtimePayloadParser.TryExtractBlockHash(payload, out var blockHash))
                        return false;

                    realtimeEvent = new BitailsRealtimeEvent(
                        BitailsRealtimeEventKind.BlockConnected,
                        topic,
                        observedAt,
                        BlockHash: blockHash);
                    return true;
                case BitailsRealtimeTopicCatalog.ZmqRemovedFromMempoolBlockTopic:
                case BitailsRealtimeTopicCatalog.ZmqDiscardedFromMempoolTopic:
                    if (!BitailsRealtimePayloadParser.TryExtractTxId(payload, out var txId))
                        return false;

                    realtimeEvent = new BitailsRealtimeEvent(
                        BitailsRealtimeEventKind.TransactionRemoved,
                        topic,
                        observedAt,
                        TxId: txId);
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryCreateEventFromJson(
            string topic,
            SocketIOClient.SocketIOResponse response,
            out BitailsRealtimeEvent realtimeEvent)
        {
            realtimeEvent = null;

            JsonElement payload;
            try
            {
                payload = response.GetValue<JsonElement>(0);
            }
            catch
            {
                return false;
            }

            var observedAt = DateTimeOffset.UtcNow;
            switch (topic)
            {
                case BitailsRealtimeTopicCatalog.ZmqRawTx2Topic:
                    if (!BitailsRealtimePayloadParser.TryExtractRawHex(payload, out var rawHex))
                        return false;

                    BitailsRealtimePayloadParser.TryExtractTxId(payload, out var txId);
                    realtimeEvent = new BitailsRealtimeEvent(
                        BitailsRealtimeEventKind.TransactionAdded,
                        topic,
                        observedAt,
                        TxId: txId,
                        RawTransaction: Convert.FromHexString(rawHex));
                    return true;
                case BitailsRealtimeTopicCatalog.ZmqRemovedFromMempoolBlockTopic:
                case BitailsRealtimeTopicCatalog.ZmqDiscardedFromMempoolTopic:
                    if (!BitailsRealtimePayloadParser.TryExtractTxId(payload, out var removedTxId))
                        return false;

                    BitailsRealtimePayloadParser.TryExtractRemoveReason(payload, out var reason);
                    BitailsRealtimePayloadParser.TryExtractCollidedWithTransaction(payload, out var collidedWith);
                    BitailsRealtimePayloadParser.TryExtractBlockHash(payload, out var blockHash);

                    realtimeEvent = new BitailsRealtimeEvent(
                        BitailsRealtimeEventKind.TransactionRemoved,
                        topic,
                        observedAt,
                        TxId: removedTxId,
                        RemoveReason: reason,
                        CollidedWithTransaction: collidedWith,
                        BlockHash: blockHash);
                    return true;
                case BitailsRealtimeTopicCatalog.ZmqHashBlock2Topic:
                    if (!BitailsRealtimePayloadParser.TryExtractBlockHash(payload, out var connectedBlockHash))
                        return false;

                    realtimeEvent = new BitailsRealtimeEvent(
                        BitailsRealtimeEventKind.BlockConnected,
                        topic,
                        observedAt,
                        BlockHash: connectedBlockHash);
                    return true;
                default:
                    return false;
            }
        }
    }
}
