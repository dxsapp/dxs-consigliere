---
created: 2026-05-18
type: design-brief
audience: design-agent (Claude Design / Figma)
status: discovery-complete, awaiting design
supersedes: existing admin-ui stub in src/admin-ui/ (do NOT use as reference)
---

# Consigliere Admin UI — vNext Design Brief

Hi-fi design ask for a brand-new operator admin for the
Consigliere BSV thin-node observer. The existing admin-ui stub
in `src/admin-ui/` is being abandoned; design from scratch.

## 1. Context

Consigliere is a BSV thin-node observer that ingests
transactions from three sources (P2P pool / Bitails REST /
JungleBus REST), maintains a headers chain, runs an idempotent
broadcast pipeline, and tracks per-source observation metrics.
Six engineering waves shipped W1-W6 (program closeout:
`docs/stream-tasks/consigliere-thin-node-observer-program/evidence/closeout.md`).

The thin-node exposes ~15 admin REST endpoints under
`/api/admin/*` (auth: `AdminAuthDefaults.Policy`) and pushes
real-time events via SignalR (`wallethub`). The admin UI is
the human surface on top of those.

## 2. Users

Two depth modes, NOT separate roles:

| Mode | Who | Job-to-be-done |
|---|---|---|
| **Default (biz-op)** | Business operator / customer support | Look up entity state (tx / address / token); see active alerts as a "system unhealthy → escalate" signal |
| **Advanced (dev)** | Engineer / SRE | System state + log analysis: P2P pool, peer scoring, source metrics, alert journal, headers chain, raw documents |

Switch: a visually-separated **"System"** section in the
sidebar, always visible (not behind a toggle), with a small
"DEV" chip next to each System item. Biz-ops won't click it;
engineers go there directly.

## 3. Operating context

- **Always-open dashboard on a second monitor.** The UI must
  read at a glance from across a desk. Real-time push is
  critical; no manual refresh.
- **Multi-monitor / always-on** → dark theme as default.
- **No tablet middle ground.** Desktop primary
  (1440px+, 1920px+ optimal) + mobile responsive (sidebar
  drawer, screen stack). No iPad-specific layout.

## 4. Information architecture

### Sidebar — Operator section

1. **Dashboard** — landing
2. **Transactions** — tx lookup + lifecycle viewer
3. **Broadcast Queue** — in-flight tx (live kanban)
4. **Addresses** — address lookup + state
5. **Tokens** — DSTAS / native token lookup + state
6. **Alerts** — active alerts + history journal

### Sidebar — System section (visually separated, always visible)

7. **P2P Pool** — peers + scoring + rotation
8. **Source Metrics** — per-source observation + visibility deltas
9. **Headers Chain** — tip + recent + reorg history
10. **Broadcast Inspector** — submit form + lifecycle watch
11. **Configuration** — read + tune (hot-reload pre-wired)
12. **Logs / Raw** — journal inspection, raw documents
13. **Providers** — source-policy / capability matrix
14. **Setup** — first-run / environment

Each System item shows a small "DEV" chip in the sidebar.

### Header (global, all screens)

- **Smart search bar** — auto-recognises input format (txid hash
  → tx; address base58 → address; height integer → block;
  token-id → token). No type selector; Enter goes to the
  resolved entity.
- **Alert badge** — count of active (unacknowledged) alerts.
  Click → drops to dedicated Alerts screen.
- **Connection status** — SignalR up/down indicator.
- **Environment tag** — mainnet / testnet / dev.

## 5. Per-screen briefs (priority order)

### 5.1 Dashboard (hi-fi mock #1, highest priority)

The screen that's always open. Composition:

- **Hero (top-of-page)**: composite system-health indicator + a
  sparkline of mempool tx-rate next to it. Glanceable from
  across a desk. Bigger than anything else on the page.
- **Search prompt** centered below hero (yes, in addition to the
  header bar — double-entry by operator request). Recent
  lookups underneath as chips.
- **Activity stream** (right column or below): recent broadcasts
  (our outgoing tx, newest first, with state badge) + per-source
  visibility feed (live rate per source — "P2P: 1.2 tx/s,
  Bitails: 1.1, JungleBus: 0.3"). Early warning for source
  dropout.
- **Sparkline panel** (next to activity): 1-2 inline sparklines
  (mempool rate, pool size over time). Compact, not full
  charts.

Do NOT include on the Dashboard:
- Recent blocks list (lives on Headers Chain screen)
- Recent alerts list (already in header badge + toast + Alerts
  screen)
- Full charts / time-range pickers (lives on Source Metrics)

### 5.2 Transactions / Address / Token detail (hi-fi mock #2)

Triggered by lookup from header search. All three entities
share the same general layout — a **vertical timeline** as the
hero element:

- **Tx timeline**: Validated → Dispatching → PeerRelayed →
  Mined → Confirmed. Each stage shows transition timestamp,
  source-of-record, error (if any). The active stage is
  highlighted.
- **Address timeline**: balance changes / tx history.
- **Token timeline**: mint / burn / transfer events.

Below the timeline: secondary panels with entity metadata
(amount, sources observed, raw hex, projection state).

### 5.3 Broadcast Queue (hi-fi mock #3)

In-flight tx live view. **Kanban layout**, 3 columns:

| Validated | Dispatching | PeerRelayed |
|---|---|---|

Cards move between columns in real time as state transitions
arrive over SignalR. Each card: txid, age in flight, last
transition timestamp, source-of-record. Stale-highlight for
cards >5 min in Dispatching (this is the bottleneck signal).

Card click → opens Tx detail (timeline). Right-click /
"⋯" menu → **"Force rebroadcast"** (only destructive action in
the UI — see §7).

### 5.4 Alerts (hi-fi mock #4)

Two-section vertical:

- **Top: Active alerts** — currently-firing alerts
  (unacknowledged). Cards with rule type, detail, fire
  timestamp, context table. Background = severity colour.
- **Bottom: History journal** — chronological log of past
  fires. Scrollable, filterable by rule type + time range.

No ack / dismiss action in this wave (alerts journal is
read-only; operator just sees them and escalates).

### 5.5 P2P Pool (hi-fi mock #5 — System section's marquee)

Layout challenge: fit two visuals on one screen.

- **Left (60-70% width): active-peers table** — one row per
  active peer with: key (host:port), current score (0-100,
  colour-graded), score-component breakdown (latency_penalty
  / reject_penalty / relay_back_bonus shown inline as
  mini-bars or sparkline), PingP95, RelayBack count,
  RejectByClass tally, last-disconnect-reason.
- **Right (30-40% width): subnet/24 diversity** — donut or
  treemap of how peers cluster by /24 subnet. Sparse cluster
  = healthy; one fat slice = concentration risk.

### 5.6 Tokens-via-tokens (lower priority — design-tokens only)

For screens 6-14 the design agent ships **design tokens +
component patterns** (no per-screen hi-fi mockups). Engineering
fills them in from the patterns. Brief shape only:

- **Source Metrics** — large per-source chart panel + per-source
  visibility-counter cards.
- **Headers Chain** — height-sorted list of recent headers +
  tip card + reorg event log.
- **Broadcast Inspector** — rawHex paste textarea + Submit +
  result lifecycle (mirrors Tx detail's timeline component).
- **Configuration** — sectioned config view (per
  BsvP2pConfig.Alert / .Inbound / .TxPolicy etc) with inline
  edit affordance per field (most fields read-only until
  backend ships hot-reload endpoint).
- **Logs / Raw** — journal browser + raw-document JSON viewer.
- **Providers** — capability matrix (sources × capabilities).
- **Setup** — first-run wizard.

## 6. Interaction patterns

### 6.1 Real-time

- **SignalR everywhere it's available** (block tip, broadcast
  state transitions, alert fires).
- Polling fallback when SignalR disconnects.
- **Stale-state visualisation**: when a widget's data source
  goes stale (SignalR drop OR polling failure), the widget
  gets a semi-transparent grey overlay — no global banner.
  Per-widget granularity so the rest of the page stays usable.

### 6.2 Search

Smart input. After Enter, the controller:
1. Hash regex match (64 hex) → resolve as tx first, fallback to
   block hash.
2. Integer match → block height.
3. Base58 / bech32 match → address.
4. Otherwise → assume token-id.

If ambiguous OR not found → render a "did you mean ..." panel
with the candidate entities and let the operator pick.

Recent lookups stored client-side (localStorage) and shown as
chips below the search bar on Dashboard.

### 6.3 Time presentation

**Both formats visible**, e.g. `14:23:11 UTC (2 min ago)`. UTC
absolute first, relative in parentheses. No hover-to-reveal.

### 6.4 Empty / loading / error states

- **Loading**: skeleton screens per widget (no global spinner).
- **Empty**: short copy explaining what would normally be here
  + a CTA chip (e.g. "No alerts firing — system healthy" with
  a chip "Show history").
- **Error**: inline per-widget error with a Retry button. No
  full-page error pages.

### 6.5 Toast / push UX

- New alert → corner toast with severity colour, auto-dismiss
  in 8s, click → navigates to Alerts screen.
- Stuck broadcast (>5 min in Dispatching) → toast with "Force
  rebroadcast" button.

## 7. Destructive actions

**Exactly one destructive action ships in this UI:**

- **Force rebroadcast** on a stuck tx (Broadcast Queue card "⋯"
  menu + toast CTA). Confirmation modal: "Re-announce {txid}?
  Current state: Dispatching, {ageMin} minutes old."

Everything else is read-only in this wave:
- No manual peer eviction (rotation is automatic).
- No alert ack / dismiss (journal is read-only).
- No config edit yet (read view; the hot-reload endpoint is a
  follow-up).

Broadcast Inspector's submit form is NOT a destructive action
in this taxonomy — it creates a new tx, doesn't mutate
existing state.

## 8. Design system constraints

| Constraint | Decision |
|---|---|
| Theme | Both with toggle; system-default → dark. |
| Density | Dense ↔ Comfortable toggle, per-user pref persisted client-side. Dense = Linear/Bloomberg; Comfortable = Stripe. |
| Devices | Desktop primary (1440px+) + responsive mobile (sidebar drawer + screen stack). No dedicated tablet layout. |
| Real-time chrome | Stale per-widget overlay (gray semi-transparent), not a global banner. |
| Font stack | Open — designer's call. Code-spans for hashes / hex / config keys. |
| Iconography | Open — designer's call, prefer minimal monoline. |
| Accessibility | WCAG AA contrast in both themes; keyboard navigation on all interactive elements. |

## 9. Deliverable from design agent

**Hi-fi Figma mockups (3-5 screens):**

1. **Dashboard** (default theme + dark, dense + comfortable
   density variations — 4 frames)
2. **Transaction detail** with timeline (single frame, dark
   theme, comfortable density)
3. **Broadcast Queue** kanban (single frame, dark, comfortable)
4. **Alerts** (single frame, dark, comfortable)
5. **P2P Pool** (single frame, dark, comfortable — layout
   challenge: two visuals on one screen)

**Design tokens (for engineering to apply to screens 6-14):**

- Colour palette (light + dark; severity scale for alerts;
  score gradient 0-100; status colours: healthy/degraded/stale)
- Typography scale + line-height + weights
- Spacing rhythm (dense + comfortable)
- Shadow / elevation
- Border-radius
- Icon set + sizing
- Component primitives: card, table-row, badge, chip,
  tab, modal, drawer, toast, sparkline, timeline-stage,
  kanban-card, stale-overlay

**Interaction notes (per-screen short prose):** hover / focus /
active / disabled states; transitions on real-time data
arrival; what animates and what doesn't.

**Out of scope for design:**
- Component library code (engineering picks the framework)
- Per-screen mockups for 6-14 (tokens + patterns sufficient)
- Brand identity / logo

## 10. Backend reference

What the designer needs to know is real data:

### Admin REST endpoints

| Endpoint | Wave | Shape |
|---|---|---|
| `GET /api/admin/p2p/health` | W1 (W6 ext) | `P2pHealthDto { Bound, PoolSize, TargetPoolSize, Subnet24Diversity, ActivePeers[], InboundEnabled }` |
| `GET /api/admin/p2p/peers` | W2 | per-peer rows: key, source, ua, version, success/fail counts, timestamps, subnet24 |
| `GET /api/admin/p2p/headers/tip` | W1 | `HeadersTipDto { Hash, Height, TimestampMs, PrevHash }` |
| `GET /api/admin/p2p/headers/recent?count=N` | W1 | array of `HeadersTipDto` |
| `GET /api/admin/p2p/alerts?lastN=N&since=unixMs` | W6 | `P2pAlertResponse { Alerts[] }` of `P2pAlertEventDto { Id, AlertUnixMs, Type, Detail, Context }` |
| `GET /api/admin/metrics/sources?lastN=N` | W4 | `SourceMetricsResponse { Latest, History[] }` of `SourceMetricsSnapshot` |
| `POST /api/tx/broadcast` body `{ rawHex }` | W5 | `BroadcastReceiptDto { TxId, State, CreatedAtMs, FailReason? }` |

### SignalR events (`wallethub`)

- `OnNewBlock(BlockTipDto)` — W1
- `OnReorg(ReorgEventDto)` — W3
- `OnBroadcastStateChanged(BroadcastReceiptDto)` — W2 + W5
- (W6 alerts: poll `/api/admin/p2p/alerts?since=` — no SignalR
  push for alerts in this release)

### Alert types (W6)

`P2pAlertType` enum: `PoolSizeBelowThreshold`,
`RelayBackRateBelowThreshold`, `ReorgDepthExceeded`,
`SourceFirstDropout`. Each event carries a `Context` dict with
rule-specific keys (e.g. `poolSize`, `threshold`, `rate`,
`deltaRelayBack`, `source`, `windowMs`).

### Source identifiers

- `p2p` — direct BSV P2P observation
- `bitails` — Bitails REST/WebSocket
- `junglebus` — JungleBus REST

## 11. Existing artefacts the designer should NOT consume

- `src/admin-ui/` — abandoned stub. **Do not use as reference.**
- `docs/admin-ui/admin-ui-product-spec.md` — superseded by this brief.
- `docs/admin-ui/admin-ui-stack-and-rules.md` — engineering
  picks the stack after design lands. Ignore for design.

## 12. Open follow-ups (post-design, not blocking)

- Hot-reload config endpoint (backend) — Configuration screen
  will gain inline edit affordance once it lands.
- Alert escalation sinks (Slack / PagerDuty webhook) — Alerts
  screen will gain a "Routes" panel in a future iteration.
- Manual peer eviction endpoint — P2P Pool screen will gain
  row "⋯" menu in a future iteration.
- SPA-side log streaming for the System / Logs page — depends
  on backend log-streaming endpoint.

## 13. Reference docs (for designer's deep-dive, optional)

- `docs/platform-api/thin-node-prod-runbook.md` — operator
  recovery procedures + full configuration reference.
- `docs/platform-api/broadcast-w5-changeout.md` — broadcast
  contract + state machine for the Tx timeline.
- `docs/stream-tasks/consigliere-thin-node-observer-program/evidence/closeout.md`
  — program-level closeout with cumulative operator-facing changes.

---

End of brief. Designer: ship the 5 hi-fi mockups + tokens +
interaction notes. Engineering implements the rest from
tokens + patterns.
