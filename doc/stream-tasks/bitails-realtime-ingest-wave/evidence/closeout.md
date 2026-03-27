# Closeout

## Runtime Surface

- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/BackgroundTasks/Realtime/RealtimeIngestBackgroundTask.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/BackgroundTasks/Realtime/BitailsRealtimeIngestRunner.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/BackgroundTasks/Realtime/JungleBusRealtimeIngestRunner.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/BackgroundTasks/Realtime/BitailsRealtimeSubscriptionScopeProvider.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/BackgroundTasks/Realtime/RealtimeBootstrapPlan.cs`

## Supporting Adapter Surface

- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Infrastructure/Bitails/Realtime/IBitailsRealtimeIngestClient.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Infrastructure/Bitails/Realtime/BitailsSocketIoRealtimeIngestClient.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Infrastructure/Bitails/Realtime/BitailsRealtimePayloadParser.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Infrastructure/Bitails/Realtime/BitailsRealtimeTransactionNotification.cs`

## Wiring

- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/BackgroundTasks/AppInitBackgroundTask.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Setup/HostedTasksSetup.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Setup/IndexerOrchestrationSetup.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Setup/ExternalChainAdaptersSetup.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Bsv/Zmq/IZmqClient.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Bsv/Zmq/ZmqClient.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Bsv/Zmq/ZmqSubscriptionTopics.cs`

## Focused Proof

- runtime routing and bootstrap:
  - `/Users/imighty/Code/dxs-consigliere/tests/Dxs.Consigliere.Tests/BackgroundTasks/Realtime/RealtimeBootstrapPlannerTests.cs`
  - `/Users/imighty/Code/dxs-consigliere/tests/Dxs.Consigliere.Tests/Services/Impl/SourceCapabilityRoutingTests.cs`
- Bitails scope derivation and runtime ingest:
  - `/Users/imighty/Code/dxs-consigliere/tests/Dxs.Consigliere.Tests/BackgroundTasks/Realtime/BitailsRealtimeSubscriptionScopeProviderTests.cs`
  - `/Users/imighty/Code/dxs-consigliere/tests/Dxs.Consigliere.Tests/BackgroundTasks/Realtime/BitailsRealtimeIngestRunnerTests.cs`
- adapter support proof:
  - `/Users/imighty/Code/dxs-consigliere/tests/Dxs.Bsv.Tests/Bitails/BitailsRealtimeTopicCatalogTests.cs`
  - `/Users/imighty/Code/dxs-consigliere/tests/Dxs.Bsv.Tests/Bitails/BitailsRealtimeTransportPlannerTests.cs`
  - `/Users/imighty/Code/dxs-consigliere/tests/Dxs.Bsv.Tests/Bitails/BitailsProviderDiagnosticsTests.cs`
  - `/Users/imighty/Code/dxs-consigliere/tests/Dxs.Bsv.Tests/Bitails/BitailsRealtimePayloadParserTests.cs`

## Notes

- tracked addresses always produce address-scoped Bitails topics
- tracked tokens currently force the additional global `tx` topic because Bitails does not expose a token-specific realtime topic
- live Bitails ingest stays on the existing tx/filter pipeline; no shadow ingest path was added
- websocket is the only live transport consumed in this wave; ZMQ intent is carried only for future runtime expansion
