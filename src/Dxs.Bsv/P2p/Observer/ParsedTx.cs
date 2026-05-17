#nullable enable
using System.Collections.Generic;

namespace Dxs.Bsv.P2p.Observer;

/// <summary>
/// Tx parsing output handed to <see cref="WatchlistMatcher.Match"/>.
/// Pre-extracted so the matcher's hot path does no script parsing —
/// it only does hash lookups (see S2 acceptance criteria: matcher
/// is allocation-free on the hot path).
///
/// <para>
/// <see cref="OutputHash160s"/> — every P2PKH output Hash160 in the
/// transaction (one per P2PKH output; non-P2PKH outputs omitted).
/// </para>
/// <para>
/// <see cref="InputPayerHash160s"/> — every P2PKH input payer hash
/// computed via <c>HASH160(pubkey)</c> from the unlocking script
/// (S1 <see cref="TxScriptParser.TryParseP2pkhInputPubkey"/>). One
/// per standard P2PKH input; non-standard inputs omitted.
/// </para>
/// <para>
/// <see cref="OutputTokenIds"/> — every STAS / DSTAS token id
/// surfaced by S1 <see cref="TxScriptParser.TryParseTokenId"/>.
/// One per token output (lowercase hex). Empty list when no
/// token outputs.
/// </para>
/// </summary>
public sealed record ParsedTx(
    string TxId,
    IReadOnlyList<byte[]> OutputHash160s,
    IReadOnlyList<byte[]> InputPayerHash160s,
    IReadOnlyList<string> OutputTokenIds);
