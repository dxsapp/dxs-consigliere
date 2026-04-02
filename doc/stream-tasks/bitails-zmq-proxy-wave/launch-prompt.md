# Mission

Implement a real Bitails `zmq` realtime ingest path using Bitails' socket.io proxy service, not direct NetMQ.

# Package Path

- `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/bitails-zmq-proxy-wave/`

# Constraints

- Treat this as `Bitails ZMQ proxy over socket.io`, not direct ZMQ.
- Do not hardcode `https://zmq.bitails.io` as stable product truth.
- API key is optional and must not be required for this transport path.
- Support only confirmed topics in this wave:
  - `rawtx2`
  - `removedfrommempoolblock`
  - `discardedfrommempool`
  - `hashblock2`
- Keep the existing Bitails websocket path intact.
- Reuse existing tx/block buses and removal semantics where possible.
- Do not expand admin UX or public API scope in this wave.

# Required Execution Order

1. `BZ1 external-chain-adapters`
- add the dedicated Bitails proxy transport implementation under `/Users/imighty/Code/dxs-consigliere/src/Dxs.Infrastructure/Bitails/Realtime/`
- freeze payload/topic assumptions in code comments or tests where they matter

2. `BZ2 indexer-ingest-orchestration`
- wire `bitailsTransport = zmq` through `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/BackgroundTasks/Realtime/BitailsRealtimeIngestRunner.cs`
- map proxy events into existing buses and keep websocket path unchanged

3. `BZ3 service-bootstrap-and-ops`
- adjust config validation/startup/diagnostics only as needed to support honest proxy transport semantics

4. `BZ4 verification-and-conformance`
- add focused tests and collect explicit validation evidence

# Validation

Run the smallest command set that proves the wave:

- focused infrastructure/realtime tests, for example filtered to Bitails realtime and runner coverage
- `dotnet build /Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Dxs.Consigliere.csproj -c Release -p:UseAppHost=false` if startup/config changed
- `git diff --check`

Prefer exact test filters in closeout.

# Closeout

Update:
- `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/bitails-zmq-proxy-wave/master.md`
- `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/bitails-zmq-proxy-wave/audits/A1.md`
- `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/bitails-zmq-proxy-wave/evidence/closeout.md`

Closeout must record:
- exact implemented topics
- exact payload assumptions
- whether one shared proxy URL worked or whether split tx/block URLs still matter
- honest residuals about vendor-contract instability

# Commit / Report Expectations

- make coherent commits by slice or by tightly-coupled milestone
- report exact files changed
- do not overclaim transport support beyond the four confirmed topics
- if Bitails contract turns out to differ from current assumptions, stop and record the drift explicitly
