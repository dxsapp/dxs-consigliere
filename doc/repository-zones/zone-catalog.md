# Zone Catalog

| Zone | Paths | Primary owner stream | Backup owner stream | In scope | Out of scope | Contracts/interfaces | Criticality |
|---|---|---|---|---|---|---|---|
| `platform-common` | `src/Dxs.Common/**` | `operator/platform` | `operator/runtime` | shared background-task framework, dataflow primitives, cache, generic extensions, common exceptions | BSV parsing, Raven models, public API contracts | background task abstractions, extension method stability | high |
| `bsv-runtime-ingest` | `src/Dxs.Bsv/{BitcoinMonitor,Rpc,Zmq,Factories}/**` | `operator/runtime` | `operator/platform` | node RPC, ZMQ intake, tx/block buses, runtime filtering, throughput/backpressure behavior | script semantics, token rules, API DTOs | `ITxMessageBus`, `IBlockMessageBus`, `IRpcClient`, `IZmqClient`, `ITransactionFilter` | high |
| `bsv-protocol-core` | `src/Dxs.Bsv/{Block,Extensions,Models,Protocol,Script,Tokens,Transactions}/**`, `src/Dxs.Bsv/{Address.cs,Hash.cs,HexConverter.cs,IBroadcastProvider.cs,NBitcoinContext.cs,Network.cs,PrivateKey.cs,TokenId.cs}` | `operator/protocol` | `operator/platform` | raw transaction parsing, script reading/building, STAS/DSTAS semantics, transaction builders, token schemas | node connectivity, Raven projections, controller behavior | transaction model contracts, script/parser invariants, token-state decoding | high |
| `external-chain-adapters` | `src/Dxs.Infrastructure/**` | `operator/integration` | `operator/runtime` | Bitails, JungleBus, WhatsOnChain, websocket clients, external serialization quirks | indexer domain state, public API DTOs, BSV script rules | provider payload contracts, retry/rate-limit behavior | medium |
| `indexer-state-and-storage` | `src/Dxs.Consigliere/Data/**`, `src/Dxs.Consigliere/Services/Impl/{TransactionStore.cs,UtxoSetManager.cs,AddressHistoryService.cs}` | `operator/state` | `operator/runtime` | Raven documents, indexes, migrations, query shapes, derived STAS/DSTAS state, UTXO/history projections | transport controllers, node clients, SignalR connection orchestration | `MetaTransaction`, `MetaOutput`, Raven index/query contracts | high |
| `indexer-ingest-orchestration` | `src/Dxs.Consigliere/BackgroundTasks/**`, `src/Dxs.Consigliere/Services/Impl/{BitcoindService.cs,JungleBusBlockchainDataProvider.cs,NodeBlockchainDataProvider.cs,NetworkProvider.cs}` | `operator/runtime` | `operator/integration` | block sync, mempool orchestration, missing-data repair, chain-tip verification, provider selection | script-level parsing changes, HTTP/SignalR contract changes | background task schedules, provider selection contracts, sync-state invariants | high |
| `public-api-and-realtime` | `src/Dxs.Consigliere/{Controllers,Dto,Notifications,WebSockets}/**`, `src/Dxs.Consigliere/Services/Impl/{BroadcastService.cs,ConnectionManager.cs}` | `operator/api` | `operator/integration` | REST endpoints, SignalR contracts, outward DTOs, transaction/balance streaming, broadcast transport | Raven persistence internals, BSV parsing rules, provider-specific clients | HTTP route stability, DTO backwards compatibility, SignalR callback contracts | high |
| `service-bootstrap-and-ops` | `src/Dxs.Consigliere/{Program.cs,Startup.cs,Configs/**,Extensions/**,Logging/**,Setup/**}`, `Dxs.Consigliere.sln`, `Dockerfile`, `README.md` | `operator/platform` | `operator/runtime` | composition root, DI wiring, config loading, logging, startup, deployment/runtime packaging | protocol semantics, Raven projection rules, endpoint business behavior | configuration keys, host startup sequence, DI boundaries | medium |
| `verification-and-conformance` | `tests/**` | `operator/verification` | `operator/protocol` | parser tests, DSTAS conformance vectors, projection tests, API contract tests, backfill verification | production DI wiring, runtime provider code, release docs | test harnesses, fixtures, expected classification/state evidence | high |
| `repo-governance` | `doc/**`, `.github/**` | `operator/governance` | `operator/platform` | repo instructions, ownership docs, routing conventions, review policy, CODEOWNERS templates | product code, runtime behavior | zone catalog, ownership matrix, handoff contract, governance policy | medium |

## Precedence Rules

1. More specific paths win over broader paths.
2. Inside `src/Dxs.Bsv`, `bsv-runtime-ingest` owns `BitcoinMonitor`, `Rpc`, `Zmq`, and `Factories`; `bsv-protocol-core` owns the rest of `Dxs.Bsv`.
3. Inside `src/Dxs.Consigliere`, `indexer-state-and-storage`, `indexer-ingest-orchestration`, `public-api-and-realtime`, and `service-bootstrap-and-ops` must not take files from one another without a handoff note.
4. Governance files in `doc/**` and `.github/**` belong to `repo-governance`, even if they describe other zones.

## Notes

- One path has one accountable zone, even if multiple streams collaborate on it.
- `operator/*` labels are logical ownership lanes for task routing. One human operator may own all of them and spawn subagents per lane.
- Cross-zone edits require a parent task plus child tasks per zone when the change is not purely mechanical.
- `TransactionStore.cs` and `LockingScriptReader.cs` are hotspots and should be treated as high-friction files for multi-zone edits.
