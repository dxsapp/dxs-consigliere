# Dxs.Common Routing

Zone: `platform-common`

Paths:
- all files in this project

Focus:
- shared infrastructure primitives
- generic background task helpers
- dataflow abstractions
- caching and extensions

Rules:
- Keep this project domain-agnostic.
- If a helper is only used by one product zone, keep it in that zone instead of promoting it here.
- Changes to public abstractions require a quick impact scan across dependent projects.
