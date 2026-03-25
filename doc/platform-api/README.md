# Consigliere Platform API

This directory contains the product and architecture baseline for turning `Consigliere` into a selective-indexed BSV backend for wallet, trading, payment, and infrastructure workloads.

Documents:
- `consigliere-public-api-contract-v1.md` — public API contract and product promise.
- `managed-scope-model.md` — tracking model, lifecycle, completeness, and indexing guarantees.
- `source-capability-matrix.md` — upstream source capabilities, routing roles, and provider constraints.

Principles:
- `Consigliere` is not an explorer-first product.
- Public API is built around authoritative indexed state and stable realtime for managed scope.
- External providers are supply sources, not public API contracts.
