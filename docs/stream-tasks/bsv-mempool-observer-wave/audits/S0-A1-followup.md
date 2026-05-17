# Wave 2 S0 Audit A1 Follow-up

Verdict: **APPROVE**

The S0 blocker is fixed. The new source-neutral overload now propagates the journal duplicate result, the payload parameter is nullable as specified, and the duplicate path is covered by an isolated non-Raven unit test. The existing `AppendAsync(TxMessage, CancellationToken)` overload remains at its pre-W2 behavior, which is acceptable scope discipline for S0.

## Closure Table

| Finding | Status | Evidence | Residual |
|---|---|---|---|
| H1 - Source-neutral `AppendAsync` returns `true` for duplicate journal writes | **closed** | `src/Dxs.Consigliere/BackgroundTasks/TxObservationJournalWriter.cs:74-87` now captures `observationJournal.AppendAsync(...)` and returns `!result.IsDuplicate`. `tests/Dxs.Consigliere.Tests/BackgroundTasks/TxObservationJournalWriterSourceNeutralTests.cs:151-188` adds `DuplicateAppender` and asserts the overload returns `false` when the journal reports a duplicate. | None. The fix is correctly limited to the new source-neutral overload. The existing `AppendAsync(TxMessage, CancellationToken)` path at `TxObservationJournalWriter.cs:22-35` still returns `true` after append, matching pre-W2 behavior and avoiding an unrelated behavioral change. |
| M1 - Payload parameter was non-nullable despite `RawTransactionPayloadReference?` spec | **closed** | `src/Dxs.Consigliere/BackgroundTasks/TxObservationJournalWriter.cs:1` enables nullable annotations for the file. The source-neutral overload now uses `RawTransactionPayloadReference? payload` at `TxObservationJournalWriter.cs:57-61`. | No behavior concern. `#nullable enable` surfaces annotation warnings in existing helper paths that already returned null (`TryPersistPayloadAsync` at `TxObservationJournalWriter.cs:90-121`, `TryCreateObservation` at `:124-157`), but those are compile-time annotation debt, not runtime changes. |

## Cross-checks

- Scope discipline: the duplicate propagation change applies only to the new S0 overload. Not migrating the existing `TxMessage` overload is correct because S0 only froze the new source-neutral contract.
- Existing behavior under nullable: `AppendAsync(TxMessage)` still calls `TryCreateObservation`, persists payload when possible, appends, and returns `true`; `TryPersistPayloadAsync` still returns null when there is no raw tx or persistence is disabled; `TryCreateObservation` still returns `false` when the message type is unsupported.
- Test isolation: `DuplicateAppender` is an in-memory stub implementing `IObservationJournalAppender`; the test file does not use `RavenTestDriver` or `GetDocumentStore`.
- Validation: `dotnet build Dxs.Consigliere.sln -c Release` completed with 0 errors. `dotnet test tests/Dxs.Consigliere.Tests/Dxs.Consigliere.Tests.csproj -c Release --no-build --filter TxObservationJournalWriterSourceNeutralTests` passed 6/6.

## New Findings

None.

## Closing Rationale

S0 now satisfies the journal contract extension requirements. S1-S8 may open.
