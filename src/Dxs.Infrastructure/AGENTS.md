# Dxs.Infrastructure Routing

Zone: `external-chain-adapters`

Paths:
- all files in this project

Focus:
- provider clients
- websocket transports
- payload normalization
- serialization quirks
- retry and rate-limit behavior

Rules:
- Do not implement indexer domain state here.
- Do not couple provider DTOs to public API DTOs.
- Keep provider-specific assumptions documented in code and tests or fixtures when available.
