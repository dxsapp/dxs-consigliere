#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;

using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Bsv.P2p.Chain;
using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Data.Models.Metrics;
using Dxs.Consigliere.Data.Models.P2p;

namespace Dxs.Consigliere.Services.P2p;

/// <summary>
/// Wave 6 S2 — pure-logic evaluator that turns one tick of P2P /
/// metrics inputs into the list of <see cref="P2pAlertEvent"/>
/// documents the poller should write.
///
/// <para>The evaluator carries one piece of in-process state: the
/// previous-tick per-peer <c>(RelayBackInv, GetDataRequested)</c>
/// lifetime-counter snapshot. Deltas across consecutive ticks drive
/// the RelayBackRate rule WITHOUT adding any new telemetry counter
/// (A1-followup C1 + A1 pass-2 H1).</para>
///
/// <para>All four rules from master.md §"Critical alert poller":
/// PoolSizeBelowThreshold, RelayBackRateBelowThreshold (with
/// zero-sample-window suppression), ReorgDepthExceeded,
/// SourceFirstDropout.</para>
/// </summary>
public sealed class P2pAlertEvaluator
{
    private static readonly string[] KnownSources =
    {
        TxObservationSource.P2p,
        TxObservationSource.Bitails,
        TxObservationSource.JungleBus,
    };

    private Dictionary<string, (long RelayBackInv, long GetDataRequested)> _previousByPeer = new();
    private bool _haveBaseline;

    /// <summary>
    /// Test seam: forget the previous-tick baseline. The very next
    /// <see cref="Evaluate"/> call records baseline and does NOT fire
    /// the relay-back rule (A1-followup C1 behaviour).
    /// </summary>
    public void ResetState()
    {
        _previousByPeer = new Dictionary<string, (long, long)>();
        _haveBaseline = false;
    }

    public IReadOnlyList<P2pAlertEvent> Evaluate(P2pAlertEvaluatorInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var results = new List<P2pAlertEvent>();
        var nowMs = input.Now.ToUnixTimeMilliseconds();

        // Each rule that fires picks a unique nowMs offset to avoid
        // colliding document ids when multiple rules fire on the
        // same tick. Offsets are 0..3 ms — well below the 60 s poll
        // cadence so timestamps stay monotonically increasing across
        // ticks.
        var idOffset = 0;

        // Rule 1: PoolSizeBelowThreshold
        if (input.PoolSize < input.Config.MinPoolSize)
        {
            results.Add(BuildAlert(
                nowMs + idOffset++,
                P2pAlertType.PoolSizeBelowThreshold,
                $"Pool size {input.PoolSize} below configured minimum {input.Config.MinPoolSize}",
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["poolSize"] = input.PoolSize.ToString(CultureInfo.InvariantCulture),
                    ["threshold"] = input.Config.MinPoolSize.ToString(CultureInfo.InvariantCulture),
                }));
        }

        // Rule 2: RelayBackRateBelowThreshold — delta-based.
        if (EvaluateRelayBackRate(input, out var relayDetail, out var relayContext))
        {
            results.Add(BuildAlert(
                nowMs + idOffset++,
                P2pAlertType.RelayBackRateBelowThreshold,
                relayDetail,
                relayContext));
        }

        // Rule 3: ReorgDepthExceeded — single point-in-time signal.
        if (input.LastDegradedReorgAt is { } reorgAt)
        {
            var ageMs = (input.Now - reorgAt).TotalMilliseconds;
            if (ageMs >= 0 && ageMs <= input.Config.ReorgDepthWindowMs)
            {
                results.Add(BuildAlert(
                    nowMs + idOffset++,
                    P2pAlertType.ReorgDepthExceeded,
                    $"Degraded reorg observed {ageMs:F0} ms ago (within {input.Config.ReorgDepthWindowMs} ms window)",
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["reorgAt"] = reorgAt.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture),
                        ["ageMs"] = ((long)ageMs).ToString(CultureInfo.InvariantCulture),
                        ["windowMs"] = input.Config.ReorgDepthWindowMs.ToString(CultureInfo.InvariantCulture),
                    }));
            }
        }

        // Rule 4: SourceFirstDropout — snapshot-history delta.
        foreach (var ev in EvaluateSourceFirstDropouts(input))
        {
            ev.AlertUnixMs = nowMs + idOffset;
            ev.Id = P2pAlertEvent.BuildId(ev.AlertUnixMs);
            idOffset++;
            results.Add(ev);
        }

        return results;
    }

    private bool EvaluateRelayBackRate(
        P2pAlertEvaluatorInput input,
        out string detail,
        out Dictionary<string, string> context)
    {
        detail = string.Empty;
        context = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        long sumRelay = 0;
        long sumGetData = 0;
        var nextState = new Dictionary<string, (long, long)>(input.PeerTelemetry.Count);

        foreach (var (peerKey, telemetry) in input.PeerTelemetry)
        {
            nextState[peerKey] = (telemetry.RelayBackInvCount, telemetry.GetDataRequestedCount);

            if (!_previousByPeer.TryGetValue(peerKey, out var previous))
                continue; // new peer this tick — no delta available

            // Negative deltas (counter reset on session reconnect) are
            // clamped to 0 so a reset doesn't subtract from the rate.
            var deltaRelay = Math.Max(0, telemetry.RelayBackInvCount - previous.RelayBackInv);
            var deltaGet = Math.Max(0, telemetry.GetDataRequestedCount - previous.GetDataRequested);
            sumRelay += deltaRelay;
            sumGetData += deltaGet;
        }

        // Always advance baseline after computing the deltas — even
        // if we just recorded baseline this very tick.
        _previousByPeer = nextState;

        if (!_haveBaseline)
        {
            // First tick after startup / reset — baseline only.
            _haveBaseline = true;
            return false;
        }

        // A1 pass-2 H1 fix: zero-sample window is a no-op. Without
        // an inv-request denominator there is no rate to evaluate.
        if (sumGetData == 0) return false;

        var rate = (double)sumRelay / sumGetData;
        if (rate >= input.Config.MinRelayBackRate) return false;

        detail = $"Relay-back rate {rate:F3} below threshold {input.Config.MinRelayBackRate:F3} "
            + $"(ΔrelayBack={sumRelay}, ΔgetDataRequested={sumGetData})";
        context["rate"] = rate.ToString("F6", CultureInfo.InvariantCulture);
        context["threshold"] = input.Config.MinRelayBackRate.ToString("F3", CultureInfo.InvariantCulture);
        context["deltaRelayBack"] = sumRelay.ToString(CultureInfo.InvariantCulture);
        context["deltaGetDataRequested"] = sumGetData.ToString(CultureInfo.InvariantCulture);
        return true;
    }

    private static IEnumerable<P2pAlertEvent> EvaluateSourceFirstDropouts(P2pAlertEvaluatorInput input)
    {
        if (input.WindowSnapshots.Count < 2) yield break;

        var oldest = input.WindowSnapshots[0];
        var newest = input.WindowSnapshots[input.WindowSnapshots.Count - 1];
        if (newest.SnapshotUnixMs <= oldest.SnapshotUnixMs) yield break;

        // S1+S2-audit H1: the window must actually span the configured
        // dropout window. Two snapshots 30 s apart in a sparsely-
        // sampled store cannot be used to assert "zero first-seen
        // across 1 h" — they just don't have the coverage. Inclusive
        // `>=` so an exactly-windowMs span fires.
        if (newest.SnapshotUnixMs - oldest.SnapshotUnixMs < input.Config.SourceFirstDropoutWindowMs)
            yield break;

        // Per-source FirstSeen delta across [oldest, newest].
        var deltas = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        long maxDelta = 0;
        foreach (var source in KnownSources)
        {
            var oldFirstSeen = oldest.VisibilityCounters.TryGetValue(source, out var o) ? o.FirstSeen : 0;
            var newFirstSeen = newest.VisibilityCounters.TryGetValue(source, out var n) ? n.FirstSeen : 0;
            var delta = Math.Max(0, newFirstSeen - oldFirstSeen);
            deltas[source] = delta;
            if (delta > maxDelta) maxDelta = delta;
        }

        // A1-followup H1 — a system-wide quiet period (no source saw
        // anything new) is NOT a dropout; it's just an idle network.
        if (maxDelta == 0) yield break;

        foreach (var source in KnownSources)
        {
            if (deltas[source] != 0) continue;

            yield return new P2pAlertEvent
            {
                Type = P2pAlertType.SourceFirstDropout,
                Detail = $"Source '{source}' saw zero new first-seen txs across the "
                    + $"{input.Config.SourceFirstDropoutWindowMs} ms window while other sources kept moving",
                Context = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["source"] = source,
                    ["windowMs"] = input.Config.SourceFirstDropoutWindowMs.ToString(CultureInfo.InvariantCulture),
                    ["windowFromUnixMs"] = oldest.SnapshotUnixMs.ToString(CultureInfo.InvariantCulture),
                    ["windowToUnixMs"] = newest.SnapshotUnixMs.ToString(CultureInfo.InvariantCulture),
                },
            };
        }
    }

    private static P2pAlertEvent BuildAlert(
        long alertUnixMs,
        P2pAlertType type,
        string detail,
        Dictionary<string, string> context) =>
        new()
        {
            Id = P2pAlertEvent.BuildId(alertUnixMs),
            AlertUnixMs = alertUnixMs,
            Type = type,
            Detail = detail,
            Context = context,
        };
}

/// <summary>
/// Wave 6 S2 — frozen evaluator input shape. Inputs are gathered by
/// the poller, the evaluator is pure modulo its previous-tick state.
/// </summary>
public sealed record P2pAlertEvaluatorInput(
    int PoolSize,
    DateTimeOffset? LastDegradedReorgAt,
    IReadOnlyDictionary<string, PeerTelemetry> PeerTelemetry,
    IReadOnlyList<SourceMetricsSnapshot> WindowSnapshots,
    AlertConfig Config,
    DateTimeOffset Now);
