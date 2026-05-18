# Admin UI vNext — Pre-execution audit A1 prompt

Audit target:
`docs/stream-tasks/admin-ui-vnext-program/master.md` at the
program-draft commit (`882ebc0`). Run async; revisions land
before S0 opens.

---

You are auditing the implementation plan for the Consigliere
admin UI. The backend program (W1-W6) closed at commit
`955e794` with frozen contracts; this UI program builds the
human surface on top.

Read:

- `docs/stream-tasks/admin-ui-vnext-program/master.md` (the
  plan under audit)
- `docs/admin-ui/design-handoff/00-design-brief.md` (the
  design contract this plan implements)
- `docs/admin-ui/design-bundle/README.md` +
  `docs/admin-ui/design-bundle/project/theme.jsx` +
  `docs/admin-ui/design-bundle/project/Consigliere Admin.html`
  (the hi-fi deliverable)
- `/Users/imighty/Code/docs/project-stack-profiles.md`
  (workspace stack baseline — hard constraint)
- `/Users/imighty/Code/docs/frontend-principles.md` (the 22
  universal frontend principles the plan must honour)
- `/Users/imighty/Code/docs/frontend-audits-playbook.md` (the
  audit cadence the implementation will eventually be measured
  against)
- `docs/stream-tasks/consigliere-thin-node-observer-program/evidence/closeout.md`
  (the closed backend program — frozen REST + SignalR contracts
  this UI consumes, end-of-program residuals to be aware of)

Cross-validate against the actual repo state:

- `src/admin-ui/` (the existing stub the plan deletes at S0;
  inspect what's there and whether any callers exist)
- `src/Dxs.Consigliere/Controllers/*.cs` (admin REST surface
  the UI consumes)
- `src/Dxs.Consigliere/WebSockets/` (SignalR hub the UI
  consumes)
- `src/Dxs.Consigliere/Data/Models/P2p/P2pAlertEvent.cs`,
  `OutgoingTransaction.cs`, `OutgoingTxState.cs`, etc. (DTO
  source shapes)

## Audit dimensions

Score each finding **CRITICAL** / **HIGH** / **MEDIUM** /
**LOW**.

1. **Stack alignment.** Does the plan honour
   `project-stack-profiles.md` §"Default Frontend Baseline"
   verbatim (React 19 + Vite 7 + TS + MUI + MUI X + MobX +
   mobx-persist-store + react-router-dom 7 + framer-motion +
   pnpm), with no unstated additions or omissions?

2. **Frontend-principles coverage.** The plan's 12 Core Rules
   are derived from `frontend-principles.md`'s 22 principles +
   the design brief. Are any of the 22 principles materially
   uncovered or contradicted? Call out specific principle
   numbers (e.g. "§16 route-driven hydration is asserted but
   the slice ledger doesn't show where the route loaders
   actually live").

3. **Slice ledger completeness.** Do S0-S12 cover all 14
   screens from the design brief + foundation + closeout?
   Map each screen to a slice; flag any screen missing.

4. **Slice dependencies.** Is `S0 → S1 → S2 → S3 → (S4..S10) →
   S11 → S12` a sound DAG? Are S4-S10 actually parallelizable
   given they share the AppBar / Drawer / theme / API client
   from S2 + S3? Are there hidden ordering constraints
   (e.g. Alerts toast depends on SignalR client S3 + header
   badge S2)?

5. **Foundational slice gates.** Is `S0 + S2 + S3` the right
   set for slice-level audits, or should S1 (theme) also gate?
   The W6 backend program audited only the prereq slice (S0);
   the UI program audits three. Is the additional cost
   justified?

6. **Mock layer rigour.** Core Rule 12 says "mock mirrors
   real shape — no mock-only fields." Is this verifiable in
   tests (a contract-parity test that drives both modes
   against the same DTO shape)? Where does that test live in
   the slice ledger?

7. **Typed contracts source.** The backend doesn't publish an
   OpenAPI spec. The plan implies hand-written DTOs in
   `src/types/api.ts`. Is there a parity-with-backend test
   slice or a code-gen step the plan omits?

8. **SignalR push for alerts.** The design brief §10 says
   "W6 alerts: no SignalR push for alerts in this release —
   poll `/api/admin/p2p/alerts?since=`". But the design
   bundle includes a new-alert toast pattern (typical of
   push UX). Does the plan correctly note that alert toasts
   will fire on poll-delta, not push? Is this a perceptible
   UX regression worth flagging?

9. **Configuration screen scope.** Plan ships read-only this
   wave with a feature-flagged tune affordance. Is feature-
   flagging the affordance better than simply NOT designing
   the tune UX yet? (Risk: design rot if the affordance
   hides incomplete UX.)

10. **Background activity cleanup (Core Rule 6).** Plan
    asserts "every poll/timer/subscription has cleanup +
    cancellation + retry-with-backoff". Where is this
    verified per slice? Is there an integration test that
    proves no leaks across route changes?

11. **Performance budget.** "Initial JS ≤ 300 KB gzipped" —
    is this realistic given MUI material core (~150 KB
    minified) + MUI X DataGrid (~70 KB) + X Charts (~50 KB)?
    Either accept a higher budget or commit to code-splitting
    the heavy MUI X bits behind route-level lazy boundaries.

12. **A11y target on score gradient.** The 0-100 score
    gradient (red → amber → green) is the only place
    contrast might fail WCAG AA at certain stops over the
    dark background. Is this verified in S11, or should an
    earlier slice (S8 P2P Pool) own the contrast check?

13. **Mobile coverage.** Design bundle only ships a 375px
    Dashboard mobile frame. The plan claims "responsive
    mobile for all screens." Is this an implementation
    obligation the design didn't fully cover? Does the plan
    flag it as a design-handoff residual, or quietly absorb
    it?

14. **Legacy stub deletion safety.** Plan deletes
    `src/admin-ui/` at S0. Verify by grep whether ANY
    backend code (`src/Dxs.Consigliere/`) imports or
    references files in `src/admin-ui/`. If yes, S0 needs to
    sequence the deletion after backend refs are removed.

15. **CI integration.** Workspace standard is `pnpm verify`
    on every commit. Is there an existing CI for this repo
    that knows about a new `src/admin-ui/` Vite project? Plan
    should land CI config (GitHub Actions / equivalent) in
    S0, not silently inherit from the backend pipeline.

16. **Test split discipline.** Plan mentions unit, integration,
    e2e but the slice ledger doesn't show where each lives.
    Is there a documented split (unit = component render,
    integration = store + mock API, e2e = Playwright golden
    paths)? Should be locked at S0 or S2.

17. **MobX architecture detail.** Core Rule 4 says "one MobX
    store per screen; cross-screen state in parent store."
    Is the rule sufficient for cache invalidation on SignalR
    push (e.g. new block tip arrives → which stores subscribe
    and refresh)? Should the plan reference a concrete
    pattern (observable-tree / explicit-publish-subscribe)?

18. **Theme/density persistence migration.** `mobx-persist-store`
    snapshots the theme + density choice. What's the migration
    story when a token gets renamed in a future iteration?
    Plan should at least note the versioning convention.

19. **Design deviations accepted.** The plan accepts two
    bundle deviations (`shape.borderRadius: 8`,
    `typography.code: JetBrains Mono`) in DoD. Is this
    explicit-enough or should it appear in §"Product Decision"
    too?

20. **Closeout doc completeness.** S12 ships the closeout doc.
    Does the plan list every section the closeout must cover
    (delivery hashes, residuals, before/after screenshots,
    design-deviation acceptance)? Or is "lists delivery
    hashes per slice, residuals, and a before/after
    screenshot" too thin?

21. **Out-of-scope clarity.** Plan lists 5 post-program
    follow-ups. Are any of them actually in scope for this
    wave (e.g. SignalR push for alerts could be a backend
    change but a UI prep is in the plan)? Cross-check
    against the backend program closeout's "Residuals"
    section so this UI program doesn't silently absorb a
    backend gap.

22. **Smart search format detection.** Plan / brief assert
    smart-search auto-recognises tx hash vs block height vs
    address vs token. The recognition logic lives in the UI;
    is there a contract for ambiguous inputs (64-hex matches
    both tx and block hash — design says "did you mean"
    panel; is that pinned in a slice)?

23. **Stop-and-audit cost vs benefit.** 12 slices + 3 slice-
    level audits + 1 program-level A1 + 1 program-level A2
    is heavy. Is this proportionate to the scope, or should
    some slices (S5 + S6 + S7 are all similar shape) collapse
    into a single audited block?

## Verdict format

End with:

```
Verdict: APPROVE | APPROVE WITH CHANGES | MAJOR REVISION REQUIRED
Critical findings: <count>
High findings: <count>
Medium findings: <count>
Low findings: <count>
Headline: <one sentence>
```

Then per-finding detail (`C1`, `H1`, `M1`, `L1` etc.):

- Severity
- Slice (or `program-level`)
- Issue (1-2 sentences with file:line where applicable)
- Recommended fix (concrete)
