// Wave 5 S5: legacy multi-provider tests
// (Broadcast_Succeeds_WhenAnyConfiguredProviderAccepts,
//  Broadcast_Fails_WhenAllConfiguredProvidersReject,
//  Broadcast_FailsClearly_WhenNoBroadcastProviderIsConfigured)
// were deleted along with the legacy Broadcast(string)/Broadcast(Transaction)
// overloads in S2. Coverage of the unified P2P-driven BroadcastAsync
// path lives in:
//   - tests/Broadcast/IBroadcastServiceShapeTests.cs (interface shape)
//   - tests/Broadcast/BroadcastUnificationGrepTests.cs (grep regression)
// and the existing W2 Gate-3 tests for OutgoingTransactionMonitor +
// TxRelayCoordinator (which exercise the announce / lifecycle path).
//
// This file is intentionally left empty (placeholder) so the test
// project's directory structure is preserved; a follow-up commit can
// delete it entirely if desired.
