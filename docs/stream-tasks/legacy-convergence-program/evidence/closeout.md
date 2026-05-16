# Closeout

## Closed Waves

1. `LC1` Capability Contract Cleanup
2. `LC2` Raw Tx Convergence
3. `LC3` Validation Capability Convergence
4. `LC4` Historical Scan Truthfulness
5. `LC5` Broadcast Multi-Target
6. `LC6` Dead Legacy Removal
7. `A1` Program Closeout Audit

## Runtime / Product Outcomes

- raw transaction consumers now route through one internal raw-tx service
- public `(D)STAS` validation results expose local validation semantics more honestly
- historical scan configuration is truthful for v1
- broadcast uses multi-target `any_success`
- long-unconfirmed rebroadcast now reuses the canonical broadcast service instead of duplicating provider logic

## Key Proof

Validation commands used during closeout:

```bash
dotnet test /Users/imighty/Code/dxs-consigliere/tests/Dxs.Consigliere.Tests/Dxs.Consigliere.Tests.csproj -c Release -p:UseAppHost=false --filter "FullyQualifiedName~TransactionControllerValidateStasTests|FullyQualifiedName~TransactionQueryServiceValidateStasTests|FullyQualifiedName~TransactionQueryServiceLifecycleTests|FullyQualifiedName~VNextDstasFullSystemValidationTests|FullyQualifiedName~TrackedHistorySyncTests|FullyQualifiedName~SourceCapabilityRoutingTests|FullyQualifiedName~ConsigliereConfigBindingTests|FullyQualifiedName~BroadcastServiceTests"
```

Result:
- `Passed: 44`

```bash
pnpm typecheck && pnpm build
```

Run in:
- `/Users/imighty/Code/dxs-consigliere/src/admin-ui`

Result:
- passed

## Commit Trail

- `cf75372` `docs(product): add legacy convergence program`
- `89e4e4a` `docs(product): align capability contract wording`
- `b2ee0e4` `feat(runtime): converge raw transaction fetching`
- `b77870f` `feat(api): broadcast to all configured providers`
- `f614373` `feat(validation): align local stas verdict semantics`
- `2c55564` `feat(runtime): make historical scans truthful in v1`
