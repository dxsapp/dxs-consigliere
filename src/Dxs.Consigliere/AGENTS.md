# Dxs.Consigliere Routing

This project is split into four main zones:

- `indexer-state-and-storage`
  - Paths: `Data/**`, `Services/Impl/TransactionStore.cs`, `Services/Impl/UtxoSetManager.cs`, `Services/Impl/AddressHistoryService.cs`
  - Focus: Raven models, indexes, projections, state recompute, UTXO/history

- `indexer-ingest-orchestration`
  - Paths: `BackgroundTasks/**`, `Services/Impl/BitcoindService.cs`, `Services/Impl/JungleBusBlockchainDataProvider.cs`, `Services/Impl/NodeBlockchainDataProvider.cs`, `Services/Impl/NetworkProvider.cs`
  - Focus: sync loops, block/mempool orchestration, provider selection, repair flows

- `public-api-and-realtime`
  - Paths: `Controllers/**`, `Dto/**`, `Notifications/**`, `WebSockets/**`, `Services/Impl/BroadcastService.cs`, `Services/Impl/ConnectionManager.cs`
  - Focus: REST/SignalR contracts and outward behavior

- `service-bootstrap-and-ops`
  - Paths: `Program.cs`, `Startup.cs`, `Configs/**`, `Extensions/**`, `Logging/**`, `Setup/**`
  - Focus: DI, config surface, startup, runtime packaging

Rules:
- Do not add controller-specific logic to data/store classes.
- Do not let background tasks shape public DTOs directly.
- Keep `Startup.cs` as composition root; move business logic into owned zones.
- Any protocol-field rollout must sequence:
  - `bsv-protocol-core`
  - `indexer-state-and-storage`
  - `public-api-and-realtime`
  - `verification-and-conformance`

Hot files:
- `Services/Impl/TransactionStore.cs`
- `BackgroundTasks/Blocks/BlockProcessBackgroundTask.cs`
- `Services/Impl/ConnectionManager.cs`
- `Startup.cs`
