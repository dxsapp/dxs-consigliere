# Cache Coupling and Envelope Backfill Slices

- `J02`: add cache invalidation domain telemetry plus projection lag/backfill status readers without widening cache backend scope.
- `J03`: expose richer cache runtime status through existing ops/admin surfaces and DTOs.
- `J04`: add a bounded background rewrite task for `AddressProjectionAppliedTransactionDocument` records missing history envelopes.
- `J05`: add regression coverage for telemetry, lag/backfill reporting, and envelope rewrite behavior.
- `A1`: audit.
- `J06`: closeout.
