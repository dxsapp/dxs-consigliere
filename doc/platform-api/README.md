# Consigliere Platform API

This directory contains the product and architecture baseline for turning `Consigliere` into a selective-indexed BSV backend for wallet, trading, payment, and infrastructure workloads.

Documents:
- `consigliere-public-api-contract-v1.md` — public API contract and product promise.
- `managed-scope-model.md` — tracking model, lifecycle, completeness, and indexing guarantees.
- `history-sync-model.md` — tracked history policy, readiness, coverage, and sync semantics.
- `source-capability-matrix.md` — upstream source capabilities, routing roles, and provider constraints.
- `source-config-examples.md` — canonical `appsettings`-style source configuration examples.
- `implementation-roadmap.md` — phased implementation plan and mandatory write-path rework.
- `vnext-implementation-slices.md` — detailed zone-based vnext delivery plan with implementation slices.
- `history-sync-implementation-slices.md` — zone-based implementation slices for tracked history sync and readiness.

Principles:
- `Consigliere` is not an explorer-first product.
- Public API is built around authoritative indexed state and stable realtime for managed scope.
- External providers are supply sources, not public API contracts.
- Source configuration follows a preferred-mode model with capability-level overrides.

Supported preferred modes in `v1`:
- `node`
- `junglebus`
- `bitails`
- `hybrid`

`hybrid` means one primary source plus multiple fallbacks.

Future source direction:
- `network_connector` as a narrow direct-to-network source for realtime ingest and chain visibility

Source configuration in `v1` is split into:
- `providers`
- `routing`
- `capabilities`

Canonical documented configuration examples should use `appsettings`-style JSON.

The top-level configuration path for the source model is `Consigliere:Sources`.

Storage for internal artifacts such as payloads should live under a separate `Consigliere:Storage` section.

`Consigliere:Storage` should be a general storage envelope, with `RawTransactionPayloads` as the first concrete child in `v1`.

Operator-facing runtime templates now live in:
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/appsettings.vnext.node.example.json`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/appsettings.vnext.hybrid.example.json`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/appsettings.vnext.provider-only.example.json`
