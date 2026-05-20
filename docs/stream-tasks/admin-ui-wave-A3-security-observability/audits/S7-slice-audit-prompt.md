# wave-A3 S7 — slice-audit prompt

Audit target: wave-A3 S7 (Operator runbook) on
`codex/consigliere-vnext`. Diff range:
`<S6-fold commit>..<S7 commit>`.

---

You are auditing **the final slice of wave-A3**. S7 ships
the operator-facing handbook the slice's intent calls for:
"A non-engineer (the customer's SRE / DevOps person) can
stand up Consigliere, monitor it, recover from outages,
rotate secrets, restore from backup — without reading
source code."

Read first:

- `docs/stream-tasks/admin-ui-wave-A3-security-observability/master.md`
  (S7 row marked done; Delivery Notes updated)
- `docs/stream-tasks/admin-ui-wave-A3-security-observability/slices.md`
  § S7 (intent · owned paths · exact task · what-not-to-do
  · validation · completion signal)
- `docs/stream-tasks/admin-ui-wave-A3-security-observability/launch-prompt.md`
  (closeout expectations: a non-engineer reads ONLY the
  runbook + stands up the stack < 45 min stopwatch)

Cross-validate against the deliverable:

- `docs/runbook.md` — full 8-section operator handbook.
  The intro establishes the angle-bracket placeholder
  convention (`<example.com>`, `<admin>`, `<txid>`) so no
  real-looking sample data leaks into the document. TOC
  lists every section.
  - **§1 First-time deployment** — Ubuntu/Debian VM
    prerequisites, Docker install, repo clone, the four-
    secret `.env` (`CADDY_DOMAIN`, `CADDY_EMAIL`,
    `BSV_NODE_RPC_PASSWORD`, `RAVEN_PASSWORD`), prod-profile
    bring-up via `docker compose -f compose.yml -f
    compose.prod.yml --profile prod up -d --build`, smoke
    checks (HSTS, setup status, liveness).
  - **§2 First-run setup wizard** — four-step UX walkthrough
    (admin account, providers, block sync, review), what
    the wizard writes to where (Raven setup-bootstrap doc
    + on-disk providers.json), troubleshooting cookie
    issues behind a non-Caddy reverse proxy.
  - **§3 Scaling guidance** — three-tier vertical sizing
    table (light / medium / heavy), disk-pressure
    monitoring, explicit horizontal-scaling deferral with
    escalate-to-engineering path for pruning.
  - **§4 Monitoring + alerting** — three-endpoint summary
    table, JSON response shape, sample k8s probe block,
    recommended alert thresholds (page on vs. notify on),
    Caddy access-log streaming.
  - **§5 Log streaming** — admin UI `/logs` live tail
    (sanitizer rule list documented), optional external
    Serilog sink via `appsettings.Production.json`
    (Grafana Loki example, no rebuild required).
  - **§6 Audit log forensics** — sample forensic workflows
    table, retention semantics (365d `@expires`), export
    procedure via Raven Studio + Smuggler.
  - **§7 Secrets rotation** — admin password
    (re-run wizard), provider API keys (UI path + direct
    edit fallback), RavenDB password, TLS cert force-
    renewal with the Let's Encrypt rate-limit warning.
  - **§8 Disaster recovery** — Smuggler-based nightly
    backup cron, what's NOT in the backup (and how to
    recover it), restore procedure with 30-min RTO target,
    escalate-to-engineering exit when state is half-
    restored.
  - Appendices A-C — cookie + forwarded headers table,
    rate-limiting policy table + override env vars, CI
    grep-gate description.
- `README.md` — new "## Ops" section right before
  "## Author" pointing at `docs/runbook.md` as the
  operator-facing source of truth.
- `docs/admin-ui/design-handoff/03-prod-runbook.md`
  (DELETED) — the wave-6 stub the slice's owned paths
  called out.
- `docs/admin-ui/design-handoff/README.md` — entry #4
  rewritten to point at the new `docs/runbook.md` instead
  of the deleted stub.
- `docs/stream-tasks/admin-ui-vnext-program/evidence/closeout.md`
  — wave-A1 closeout's Residuals list strikes through the
  "backend log streaming surface" residual (closed by
  wave-A3 S4) and the "swagger codegen + ASP.NET-host
  parity" residual (closed by wave-A2 S1+S2). Adds an
  "Ops source of truth" section pointing at the runbook.
- `docs/stream-tasks/admin-ui-wave-A2-firstrun-dto/evidence/closeout.md`
  — wave-A2 closeout gets the same "Ops source of truth"
  section.

---

## What's in scope for this audit

1. **8-section completeness.** Each of the 8 slice-mandated
   sections is present in `docs/runbook.md` with numbered
   checklists / copy-pasteable commands.
2. **No internal slice / wave names leak.** The runbook
   reads as operator-facing — no `wave-A3 S0` / `wave-A1
   S10` / `S6-audit M1` references in the body. (Appendix
   labels like "Appendix A — Cookie + forwarded headers"
   are fine; they're operator-oriented.)
3. **No real-looking sample data.** Every domain, address,
   txid, etc. uses angle-bracket placeholders. The
   placeholder convention is established up front.
4. **No vague TROUBLESHOOTING.** Every procedure is
   concrete; ambiguous cases route to an explicit
   "escalate to engineering" step (§3 disk-pressure
   pruning + §8 half-restore).
5. **bash -x friendly.** Every command in §1 / §7 / §8
   either uses a documented env var (placeholder-free
   after `.env` substitution) or angle-bracket placeholders
   the operator fills in. No implicit cwd assumptions —
   §1.3 chdir into the repo before the bring-up commands.
6. **Cross-links are consistent.** The README "Ops"
   section, the design-handoff README entry #4, and both
   wave A1 + A2 closeouts point at `docs/runbook.md`. The
   deleted `03-prod-runbook.md` is no longer referenced
   anywhere that should resolve (the stub file under
   `design-bundle/` is part of the original AI-design
   inputs and stays as historical reference).
7. **No regression of any prior slice.** `pnpm verify` +
   `pnpm test:contract` still green; the backend solution
   still builds clean.

## Verdict + finding format

Verdict line first:
- `APPROVE`
- `APPROVE WITH CHANGES` — minor (L*) findings only
- `MAJOR REVISION REQUIRED` — at least one C/H/M

Findings tagged `C* | H* | M* | L*` (Critical / High /
Medium / Low). Each finding contains:
- file:line of the defect
- why it matters
- recommended fix (specific, not "consider re-architecting")

Out of scope (wave-A4):
- Wholesale hand-mirrored-interface → re-export sweep
  across the remaining ~30 Dto.cs files (S6 residual).
- Multi-instance HA (rate-limit counters / log-stream ring
  cluster awareness).
- Full axe-core CI step + per-route Lighthouse score
  (wave-A1 residual that S7 does not address).

## Validation evidence I should produce

- `dotnet build Dxs.Consigliere.sln -c Release`: clean.
- `pnpm verify`: green.
- `RAVEN_URL=http://127.0.0.1:18080 pnpm test:contract`:
  24/24 green (no regression).
- `bash scripts/secrets-lint.sh`: exits 0.
- `grep -nE "wave-A[0-9]|S[0-9]+-audit" docs/runbook.md`:
  matches the convention note in the body only (intro
  + the explicit ops-source-of-truth references in the
  closeouts; the runbook body itself reads operator-facing).
- (Stopwatch) A teammate who has not seen the codebase
  reads `docs/runbook.md` end-to-end and stands up a
  prod-profile stack on a fresh VM in < 45 minutes. This
  is the slice's completion signal; it's a one-shot
  stopwatch test that runs once at wave closeout.
