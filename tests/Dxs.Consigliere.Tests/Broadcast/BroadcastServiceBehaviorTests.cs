using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Dxs.Bsv;
using Dxs.Bsv.Models;
using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Data.Models.P2p;
using Dxs.Consigliere.Data.P2p;
using Dxs.Consigliere.Services;
using Dxs.Consigliere.Services.Audit;
using Dxs.Consigliere.Services.Impl;
using Dxs.Consigliere.Services.P2p;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Moq;

using Raven.Client.Documents;

namespace Dxs.Consigliere.Tests.Broadcast;

/// <summary>
/// Wave 5 A2 M1 fix — behavioral tests for the unified
/// <see cref="IBroadcastService.BroadcastAsync"/>. Pins:
/// - persist + receipt round-trip;
/// - announce-call observed for the valid-tx happy path;
/// - policy-invalid receipt without persisting;
/// - subsystem-disabled receipt;
/// - **no-ready-peer leaves the tx in Dispatching, NOT Failed**
///   (A2 H1 regression pin — Core Rule §2).
/// </summary>
public class BroadcastServiceBehaviorTests
{
    // The W2 fixture transaction used as a known-good raw hex.
    // (P2PKH 1 in / 1 out to a watchlist address; parses + passes
    // local policy.)
    private const string SampleTxHex =
        "0100000001c6f4b6176d3f4d6c6d9e198ba89a4eb7a1b08e6a705cc8cf0f8f2f3e3bcedf1f"
        + "000000006b4830450221009af2d63b8ef3ebf8c7a227327d8e1a89f5929087566bbb6d6f7"
        + "4a09a87e2375d022007f8cefa32f6d829bb3f8792dd11e5d8f1cb4e4f4f84f7a8d431fed0"
        + "b8ff103a4121022b698a0f0a1f1fb43fb8f33c2d72cbe7f3f8d98ef1a304681140f64e568"
        + "1970fffffffff02e8030000000000001976a91489abcdefabbaabbaabbaabbaabbaabbaab"
        + "baabba88ac0000000000000000066a040102030400000000";

    private sealed class FakeOutgoingRepo : IOutgoingTransactionRepository
    {
        public readonly ConcurrentBag<OutgoingTransaction> SaveCalls = new();
        // A2-followup N2: nullable; LastSaved is null until first Save.
        public OutgoingTransaction? LastSaved;

        // M1 duplicate-receipt test injects a pre-existing tx the repo
        // returns from GetOrNullAsync; the BroadcastService's duplicate
        // branch then returns an existing receipt without persisting.
        public OutgoingTransaction? PreExisting;

        public Task SaveAsync(OutgoingTransaction tx, CancellationToken ct = default)
        {
            // Capture a SNAPSHOT — production mutates the same instance
            // (.Touch() etc.) which would otherwise let later writes
            // overwrite our snapshot of an earlier state.
            var snap = new OutgoingTransaction
            {
                Id = tx.Id, TxId = tx.TxId, RawHex = tx.RawHex,
                State = tx.State, LastError = tx.LastError,
                ParsedSizeBytes = tx.ParsedSizeBytes,
                CreatedAtMs = tx.CreatedAtMs,
                ClientConnectionId = tx.ClientConnectionId,
            };
            SaveCalls.Add(snap);
            LastSaved = snap;
            return Task.CompletedTask;
        }
        public Task<OutgoingTransaction?> GetOrNullAsync(string txId, CancellationToken ct = default)
            => Task.FromResult(PreExisting);
    }

    private sealed class FakeAnnouncer : ITxAnnouncer
    {
        public readonly ConcurrentBag<(string TxId, string RawHex)> Calls = new();
        public int ReadyPeers { get; set; } = 1;
        public Task<int> AnnounceAsync(string txId, string rawHex, CancellationToken ct)
        {
            Calls.Add((txId, rawHex));
            return Task.FromResult(ReadyPeers);
        }
    }

    /// <summary>
    /// Test-side <see cref="IBroadcastPolicyValidator"/>. Default
    /// behaviour: accept any non-empty hex with a deterministic txid;
    /// reject empty hex. Tests override the <see cref="ResultFor"/>
    /// callback for finer control over duplicate / failure paths.
    /// </summary>
    private sealed class FakePolicyValidator : IBroadcastPolicyValidator
    {
        public Func<string, PolicyValidationResult> ResultFor { get; set; } =
            hex => string.IsNullOrEmpty(hex)
                ? PolicyValidationResult.Fail("Empty transaction hex")
                : PolicyValidationResult.Ok(
                    txId: System.Convert.ToHexString(
                        System.Security.Cryptography.SHA256.HashData(
                            System.Text.Encoding.ASCII.GetBytes(hex)))
                        .ToLowerInvariant()
                        .Substring(0, 64),
                    size: hex.Length / 2);

        // A2-followup M1 fix: tests override these to exercise the
        // duplicate-result branch in BroadcastService.BroadcastAsync.
        public Func<PolicyValidationResult, bool> IsDuplicateOverride { get; set; } = _ => false;
        public Func<PolicyValidationResult, string> ExtractTxIdOverride { get; set; } = _ => string.Empty;

        public Task<PolicyValidationResult> ValidateAsync(string rawHex, CancellationToken ct = default)
            => Task.FromResult(ResultFor(rawHex));

        public bool IsDuplicateResult(PolicyValidationResult result) => IsDuplicateOverride(result);
        public string ExtractTxIdFromDuplicate(PolicyValidationResult result) => ExtractTxIdOverride(result);
    }

    /// <summary>
    /// wave-A3 S3 — IAuditLogger fake. Default behaviour is
    /// success; tests override <see cref="ResultOverride"/> to
    /// exercise the fail-stop path.
    /// </summary>
    private sealed class FakeAuditLogger : IAuditLogger
    {
        public readonly ConcurrentBag<(string Action, string TargetId, object Context, string Username)> Calls = new();
        public Func<bool> ResultOverride { get; set; } = () => true;

        public Task<bool> RecordAsync(string action, string targetId, object context, string username = null, CancellationToken cancellationToken = default)
        {
            Calls.Add((action, targetId, context, username));
            return Task.FromResult(ResultOverride());
        }
    }

    private static (BroadcastService service, FakeOutgoingRepo repo, FakeAnnouncer announcer,
                    FakePolicyValidator validator, FakeAuditLogger audit)
        Build(bool wireP2p = true)
    {
        var audit = new FakeAuditLogger();
        var service = new BroadcastService(
            bitcoindService: Mock.Of<IBitcoindService>(),
            auditLogger: audit,
            logger: NullLogger<BroadcastService>.Instance);
        if (!wireP2p) return (service, null!, null!, null!, audit);

        var repo = new FakeOutgoingRepo();
        var announcer = new FakeAnnouncer();
        var validator = new FakePolicyValidator();

        service.PolicyValidator = validator;
        service.OutgoingStore = repo;
        service.Announcer = announcer;
        return (service, repo, announcer, validator, audit);
    }

    [Fact]
    public async Task BroadcastAsync_NoP2pSubsystem_ReturnsFailedReceipt()
    {
        var (service, _, _, _, _) = Build(wireP2p: false);

        var receipt = await service.BroadcastAsync(SampleTxHex, BroadcastSource.Operator);

        Assert.Equal(OutgoingTxState.Failed, receipt.State);
        Assert.Contains("P2P broadcast subsystem not enabled", receipt.FailReason ?? "");
    }

    [Fact]
    public async Task BroadcastAsync_PolicyInvalid_ReturnsRejectedReceipt_NotPersisted()
    {
        var (service, repo, _, _, _) = Build();

        var receipt = await service.BroadcastAsync(rawHex: "", BroadcastSource.Operator);

        Assert.Equal(OutgoingTxState.PolicyInvalid, receipt.State);
        Assert.NotNull(receipt.FailReason);
        Assert.Empty(repo.SaveCalls); // not persisted
    }

    [Fact]
    public async Task BroadcastAsync_ValidTx_PersistsValidated_ReturnsReceipt()
    {
        var (service, repo, announcer, _, _) = Build();
        // Make sure announce returns >= 1 so the no-peer branch
        // doesn't overwrite the Validated state.
        announcer.ReadyPeers = 1;

        var receipt = await service.BroadcastAsync(SampleTxHex, BroadcastSource.Operator);

        Assert.Equal(OutgoingTxState.Validated, receipt.State);
        Assert.NotNull(receipt.TxId);
        Assert.True(receipt.CreatedAtMs > 0);

        // Foreground SaveAsync persisted at Validated. The background
        // task subsequently writes Dispatching; allow a brief poll for
        // it to land.
        await PollUntil(() => repo.SaveCalls.Count >= 1);
        Assert.True(repo.SaveCalls.Count >= 1);
        // First save = Validated (synchronous foreground).
        Assert.Contains(repo.SaveCalls, s => s.State == OutgoingTxState.Validated);
    }

    [Fact]
    public async Task BroadcastAsync_AnnouncesViaTxAnnouncer_ForValidTx()
    {
        var (service, _, announcer, _, _) = Build();

        await service.BroadcastAsync(SampleTxHex, BroadcastSource.Operator);
        await PollUntil(() => announcer.Calls.Count >= 1);

        var call = Assert.Single(announcer.Calls);
        Assert.Equal(SampleTxHex, call.RawHex);
        Assert.False(string.IsNullOrEmpty(call.TxId));
    }

    [Fact]
    public async Task BroadcastAsync_DuplicateSubmission_ReturnsExistingReceipt_NoPersistOrAnnounce()
    {
        // Audit W5 A2-followup M1 fix: pin the
        // duplicate-detection branch in
        // BroadcastService.BroadcastAsync. When PolicyValidator
        // returns IsDuplicateResult=true, the service:
        // (a) extracts the existing txid via ExtractTxIdFromDuplicate,
        // (b) loads the existing OutgoingTransaction from the repo,
        // (c) returns a BroadcastReceipt mirroring the existing
        //     document's TxId / State / CreatedAtMs — WITHOUT
        //     re-persisting and WITHOUT re-announcing.
        var (service, repo, announcer, validator, _) = Build();

        // Seed the repo with an existing receipt; configure the
        // validator to flag the next submission as duplicate.
        var existing = new OutgoingTransaction
        {
            Id = OutgoingTransaction.BuildId("aabbccdd"),
            TxId = "aabbccdd",
            RawHex = SampleTxHex,
            State = OutgoingTxState.PeerRelayed,
            CreatedAtMs = 1700000000_000L,
            ParsedSizeBytes = SampleTxHex.Length / 2,
        };
        repo.PreExisting = existing;
        validator.ResultFor = _ => PolicyValidationResult.Fail("duplicate:aabbccdd");
        validator.IsDuplicateOverride = _ => true;
        validator.ExtractTxIdOverride = _ => "aabbccdd";

        var receipt = await service.BroadcastAsync(SampleTxHex, BroadcastSource.Operator);

        // Receipt mirrors the existing document.
        Assert.Equal("aabbccdd", receipt.TxId);
        Assert.Equal(OutgoingTxState.PeerRelayed, receipt.State);
        Assert.Equal(1700000000_000L, receipt.CreatedAtMs);

        // No persistence, no announce — duplicate path is read-only.
        Assert.Empty(repo.SaveCalls);
        Assert.Empty(announcer.Calls);
    }

    [Fact]
    public async Task BroadcastAsync_NoReadyPeer_StaysDispatching_NotFailed()
    {
        // Audit W5 A2 H1 regression pin: when the announcer reports
        // zero served peers, the persisted tx must stay in
        // Dispatching (a non-terminal state per
        // OutgoingTxStates.IsTerminal) so the lifecycle monitor /
        // W3 rebroadcaster can retry on the next peer-reconnect.
        // Setting Failed (the pre-fix behaviour) would have moved
        // the doc out of GetNonTerminalAsync and silently dropped
        // it forever.
        var (service, repo, announcer, _, _) = Build();
        announcer.ReadyPeers = 0;

        var receipt = await service.BroadcastAsync(SampleTxHex, BroadcastSource.Operator);
        Assert.Equal(OutgoingTxState.Validated, receipt.State);

        // Wait for background dispatch task to land its Save.
        await PollUntil(() =>
            repo.LastSaved is { State: OutgoingTxState.Dispatching });

        Assert.Equal(OutgoingTxState.Dispatching, repo.LastSaved!.State);
        Assert.False(OutgoingTxStates.IsTerminal(repo.LastSaved.State),
            $"persisted state {repo.LastSaved.State} must NOT be terminal "
            + "after a no-ready-peer announce");
        Assert.Contains("No peers available", repo.LastSaved.LastError ?? "");
        // The pre-fix bug would have set Failed; pin its absence.
        Assert.NotEqual(OutgoingTxState.Failed, repo.LastSaved.State);
    }

    [Fact]
    public async Task BroadcastAsync_ValidTx_RecordsAuditBeforePersistAndAnnounce()
    {
        // wave-A3 S3 — happy-path audit pin: the broadcast
        // happens after a clean RecordAsync, with the slice-
        // mandated context shape (rawHexLength + source).
        var (service, _, _, _, audit) = Build();

        var receipt = await service.BroadcastAsync(SampleTxHex, BroadcastSource.Operator);
        Assert.Equal(OutgoingTxState.Validated, receipt.State);

        var call = Assert.Single(audit.Calls);
        Assert.Equal(AuditActionNames.BroadcastTx, call.Action);
        Assert.Equal(receipt.TxId, call.TargetId);
        Assert.NotNull(call.Context);
        // S3-followup-2: the context carries the caller's honest
        // provenance, not a hardcoded "admin-ui".
        var ctxJson = System.Text.Json.JsonSerializer.Serialize(call.Context);
        Assert.Contains("\"source\":\"operator\"", ctxJson);
    }

    [Fact]
    public async Task BroadcastAsync_HonestSource_StampedPerCaller()
    {
        // S3-followup-2: each provenance lands its own wire string.
        var (service, _, _, _, audit) = Build();
        await service.BroadcastAsync(SampleTxHex, BroadcastSource.Wallet);
        await service.BroadcastAsync(SampleTxHex, BroadcastSource.System);
        await service.BroadcastAsync(SampleTxHex, BroadcastSource.Api);

        var sources = audit.Calls
            .Select(c => System.Text.Json.JsonSerializer.Serialize(c.Context))
            .ToList();
        Assert.Contains(sources, s => s.Contains("\"source\":\"wallet\""));
        Assert.Contains(sources, s => s.Contains("\"source\":\"system\""));
        Assert.Contains(sources, s => s.Contains("\"source\":\"api\""));
    }

    [Fact]
    public async Task BroadcastAsync_AuditFailure_NonOperator_ProceedsAnyway()
    {
        // S3-followup-2: fail-stop is operator-only. A System
        // (background monitor) broadcast whose audit write fails
        // MUST still persist + announce — otherwise a degraded
        // Raven audit store would silently halt the Wave-5
        // "always-eventually-broadcast" retry loop.
        var (service, repo, announcer, _, audit) = Build();
        audit.ResultOverride = () => false;

        var receipt = await service.BroadcastAsync(SampleTxHex, BroadcastSource.System);

        Assert.Equal(OutgoingTxState.Validated, receipt.State);
        Assert.NotEqual("audit_write_failed", receipt.FailReason ?? "");
        await PollUntil(() => announcer.Calls.Count >= 1);
        Assert.NotEmpty(announcer.Calls);
        Assert.NotEmpty(repo.SaveCalls);
    }

    [Fact]
    public async Task BroadcastAsync_AuditFailure_AbortsBroadcastWithFailedReceipt()
    {
        // wave-A3 S3 — fail-stop: if the audit write returns
        // false, the broadcast MUST NOT reach the announcer +
        // the receipt MUST report audit_write_failed.
        var (service, repo, announcer, _, audit) = Build();
        audit.ResultOverride = () => false;

        var receipt = await service.BroadcastAsync(SampleTxHex, BroadcastSource.Operator);

        Assert.Equal(OutgoingTxState.Failed, receipt.State);
        Assert.Equal("audit_write_failed", receipt.FailReason);
        Assert.Empty(repo.SaveCalls);
        Assert.Empty(announcer.Calls);
    }

    private static async Task PollUntil(Func<bool> predicate, int timeoutMs = 1_500)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (predicate()) return;
            await Task.Delay(20);
        }
    }
}
