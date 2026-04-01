Implement `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/reverse-lineage-validation-fetch-wave/master.md` end to end.

Execution rules:
- keep scope bounded to reverse-lineage validation-fetch work only
- do not turn this into broad historical discovery
- enforce JungleBus `getRawTx` throttle at `10 req/sec`
- add visited-set, traversal budget, and explicit stop reasons
- integrate with the existing validation dependency repair subsystem
- keep `Consigliere` as the only validation authority
- add focused tests and closeout artifacts

Required validation:
- `dotnet build /Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Dxs.Consigliere.csproj -c Release -p:UseAppHost=false`
- focused `dotnet test` for the new traversal/fetch behavior
- `git diff --check`

Closeout requirements:
- update wave ledger statuses in `master.md`
- add `audits/A1.md`
- add `evidence/closeout.md`
- leave the worktree clean
