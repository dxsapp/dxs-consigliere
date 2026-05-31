#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Dxs.Bsv;
using Dxs.Bsv.BitcoinMonitor;
using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Bsv.Models;
using Dxs.Bsv.P2p.Observer;
using Dxs.Consigliere.BackgroundTasks;
using Dxs.Consigliere.Data;
using Dxs.Consigliere.Services;

using Microsoft.Extensions.Logging;

namespace Dxs.Consigliere.Services.P2p;

/// <summary>
/// The single "ingest one raw transaction" pipeline, shared by every
/// source that hands the node a full tx: the P2P mempool observer
/// (<see cref="P2pMempoolIngestRunner"/>) and the self-broadcast path
/// (<c>BroadcastService</c>). It is the "decide ours / not ours, and if
/// ours credit the projection" seam:
///
///   parse → watchlist match → SaveTransaction (MetaTransaction +
///   MetaOutputs) → raw payload save → journal append.
///
/// The MetaTransaction MUST exist before the journal entry — the
/// <see cref="Dxs.Consigliere.Data.Addresses.AddressProjectionRebuilder"/>
/// credits/debits the watched address's UTXO set from it and skips
/// observations whose txid has no MetaTransaction. So a save failure
/// returns <see cref="TxIngestOutcome.SaveFailed"/> WITHOUT journaling
/// (an un-projectable record), letting the caller retry.
/// </summary>
public sealed class ObservedTxIngestor(
    WatchlistMatcher matcher,
    ITransactionStore transactionStore,
    IRawTransactionPayloadStore payloadStore,
    TxObservationJournalWriter journal,
    INetworkProvider network,
    ILogger logger,
    Dxs.Consigliere.Data.P2p.IOutgoingMempoolSightingSink mempoolSink = null)
{
    public async Task<TxIngestOutcome> IngestAsync(string txid, byte[] rawBytes, string source)
    {
        Transaction? tx;
        try { tx = Transaction.Parse(rawBytes, network.Network); }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "ingest({Source}): failed to parse {Txid}", source, txid);
            return TxIngestOutcome.ParseError;
        }
        if (tx is null) return TxIngestOutcome.ParseError;

        var parsed = ToParsedTx(tx, rawBytes, txid);
        if (matcher.Match(parsed) is MatchResult.None)
            return TxIngestOutcome.Unmatched;

        try
        {
            await transactionStore.SaveTransaction(
                tx,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                firstOutToRedeem: null);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "ingest({Source}): SaveTransaction failed for {Txid}; not journaling", source, txid);
            return TxIngestOutcome.SaveFailed;
        }

        RawTransactionPayloadReference? payloadRef = null;
        try
        {
            payloadRef = await payloadStore.SaveAsync(
                txid,
                Convert.ToHexString(rawBytes).ToLowerInvariant());
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "ingest({Source}): payload save failed for {Txid}", source, txid);
        }

        var observation = new TxObservation(
            TxObservationEventType.SeenInMempool,
            source,
            txid,
            DateTimeOffset.UtcNow);
        await journal.AppendAsync(observation, payloadRef, source);

        // If this matched tx is one of OUR broadcasts, this ingest is the
        // first independent confirmation it propagated — advance its outgoing
        // lifecycle to MempoolSeen. No-ops for any non-outgoing txid.
        if (mempoolSink is not null)
        {
            try { await mempoolSink.OnMempoolSightingAsync(txid); }
            catch (Exception ex) { logger.LogDebug(ex, "ingest({Source}): mempool-sighting hook failed for {Txid}", source, txid); }
        }

        return TxIngestOutcome.Matched;
    }

    private ParsedTx ToParsedTx(Transaction tx, byte[] rawBytes, string txid)
    {
        // Mirrors the S1 TxScriptParser contract (canonical 25-byte
        // P2PKH output check, compressed + uncompressed P2PKH input
        // pubkey derivation, defensive token parsing) — see
        // P2pMempoolIngestRunner history for why Output.Address /
        // Input.Address shortcuts are not used here.
        var outputHashes = new List<byte[]>(tx.Outputs.Count);
        var outputTokens = new List<string>();
        for (var i = 0; i < tx.Outputs.Count; i++)
        {
            var script = tx.Outputs[i].ScriptPubKey.Materialize(rawBytes);
            if (TxScriptParser.TryParseP2pkhOutput(script, out var h))
            {
                outputHashes.Add(h.ToArray());
                continue;
            }
            if (TxScriptParser.TryParseTokenId(script, network.Network, out var tokenId)
                && !string.IsNullOrEmpty(tokenId))
            {
                outputTokens.Add(tokenId);
            }
        }
        var inputHashes = new List<byte[]>(tx.Inputs.Count);
        for (var i = 0; i < tx.Inputs.Count; i++)
        {
            var input = tx.Inputs[i];
            if (input.Coinbase) continue;
            var scriptSig = input.ScriptSig.Materialize(rawBytes);
            if (TxScriptParser.TryParseP2pkhInputPubkey(scriptSig, out var h) && h is not null)
                inputHashes.Add(h);
        }
        return new ParsedTx(txid, outputHashes, inputHashes, outputTokens);
    }
}

/// <summary>Outcome of <see cref="ObservedTxIngestor.IngestAsync"/> so
/// each caller can map it to its own metrics / retry policy.</summary>
public enum TxIngestOutcome
{
    /// <summary>Raw bytes could not be parsed into a transaction.</summary>
    ParseError,

    /// <summary>Parsed fine but matched no watched address/token.</summary>
    Unmatched,

    /// <summary>Matched, but persisting the MetaTransaction failed — not
    /// journaled; the caller may retry.</summary>
    SaveFailed,

    /// <summary>Matched and fully persisted (MetaTransaction + payload +
    /// journal).</summary>
    Matched,
}
