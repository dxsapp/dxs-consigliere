# Tests Routing

Zone: `verification-and-conformance`

Focus:
- parser tests
- DSTAS/STAS conformance
- projection and state verification
- API contract checks
- backfill and replay validation

Rules:
- Tests should mirror zone boundaries from production code.
- Prefer fixtures and conformance vectors over ad hoc assertions when protocol semantics are involved.
- A failing test that reveals a contract mismatch should trigger a handoff to the owning production zone.
