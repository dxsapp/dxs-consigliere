# Slices

## `BR1` Runtime Entry Point
- identify the current hard-coded realtime entry points:
  - `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/BackgroundTasks/JungleBusMempoolMonitor.cs`
  - `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/BackgroundTasks/JungleBusSyncRequestProcessor.cs`
- add one bounded runtime-owned seam for provider-selected realtime ingest
- keep the existing `ITxMessageBus` and filter pipeline as the only downstream path

## `BR2` Adapter Bridge
- expose the minimum Bitails adapter shape needed by runtime orchestration
- consume the existing realtime topic planning seam rather than rebuilding topic logic in orchestration
- keep transport-specific details inside the adapter zone
- first implementation may stay websocket-only if `zmq` is not needed to satisfy bounded DoD

## `BR3` Wiring And Source Selection
- wire the runtime entry point through DI and hosted tasks
- make realtime ingest selection follow `SourceCapabilityRouting`
- keep JungleBus available as an optional advanced source
- surface the chosen realtime provider clearly in runtime diagnostics where cheap

## `BR4` Focused Proof
- add bounded tests or replay proof showing Bitails-originated realtime events enter the existing tx pipeline
- prove no duplicate shadow ingest path was introduced
- verify the route still respects managed-scope filtering rather than global passthrough

## `A1` Closeout Audit
- state whether Bitails is now the practical default live ingest source
- state what JungleBus still owns after the wave
- record any transport limitations, such as websocket-only first implementation
