# Cache Observability and History Paging Slices

- `H02`: expose cache observability in both ops and admin API surfaces.
- `H03`: keep config/runtime diagnostics coherent with the new observability path.
- `H04`: replace full-materialize history path with selective batch paging/count when denormalized envelope exists; preserve legacy fallback.
- `H05`: add controller/service regression coverage.
- `H06`: add benchmark evidence for the paging optimization.
- `A1`: audit.
- `H07`: closeout.
