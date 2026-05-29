# wave-A4 S2 — slice-audit prompt

Audit target: wave-A4 S2 (Real prod bring-up + soak + runbook
stopwatch — GA validation). This slice is part **ops-run**; the
audit checks the PREP + honesty, not fabricated infra results.

Read first:
- `docs/stream-tasks/admin-ui-wave-A4-turnkey-and-ga/slices.md` § S2
- `docs/stream-tasks/admin-ui-wave-A4-turnkey-and-ga/evidence/S2-ga-checklist.md`
- `docs/stream-tasks/admin-ui-wave-A4-turnkey-and-ga/evidence/S1-local-smoke.md`
- `scripts/soak-watch.sh`
- `docs/runbook.md`

## What's in scope
1. **The checklist is executable + concrete.** Every command in
   `S2-ga-checklist.md` is copy-pasteable, no implicit cwd, no
   undefined placeholder beyond the documented `<domain>` etc.
   The prod bring-up command matches what `compose.prod.yml`
   actually needs (CADDY_DOMAIN + CADDY_EMAIL).
2. **`scripts/soak-watch.sh` is correct + safe.** `bash -n`
   clean; samples readiness + container memory + (cookie-gated)
   tip height into CSV; doesn't crash on a missing cookie or a
   down host (best-effort, keeps looping).
3. **Pass criteria are real + falsifiable.** Cert issuer must be
   Let's Encrypt (not Caddy Local); memory must plateau; ready
   stays 200; tip advances; stopwatch < 45 min.
4. **Honesty.** Infra-dependent sub-steps are marked
   "[operator-run, evidence pending]" — NOT claimed as passed.
   The "Cut the first release" step is flagged as the gate for
   the S1 "pull our image" promise.
5. **No fabricated evidence.** No `evidence/*.txt`/`*.csv`
   asserting a prod result that wasn't actually produced.

## Out of scope
- The actual prod run (needs a real VM + domain). The audit
  judges whether an operator COULD execute it cleanly from the
  artifacts, not whether it was executed here.

## Verdict + finding format
Verdict first (`APPROVE` / `APPROVE WITH CHANGES` / `MAJOR
REVISION REQUIRED`); findings `C*|H*|M*|L*` with file:line, why
it matters, specific fix.
