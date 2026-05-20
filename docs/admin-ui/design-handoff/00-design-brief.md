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
Six engineering waves shipped W1-W6 (program closed).

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

**The frontend is built on MUI.** Anchor every design decision
to MUI primitives + theme tokens; do not invent new component
shapes when an MUI equivalent exists. Reference:
`01-stack-profile.md` §"Default Frontend Baseline" +
`02-frontend-principles.md` (both in this folder).

### Stack the designer must respect

- **React 19 + TypeScript + Vite 7** (engineering)
- **MUI**: `@mui/material`, `@mui/icons-material`
- **MUI X**: `@mui/x-data-grid` (every table), `@mui/x-charts`
  (every chart / sparkline)
- **Animations**: `framer-motion` (state transitions, kanban
  card moves, timeline progression)
- **State**: MobX stores; UI is a render of store state.

### Concrete MUI mappings (use these, do not redesign)

| UI element | MUI primitive |
|---|---|
| Sidebar (desktop) | `Drawer` variant `permanent` + `List` / `ListItemButton` |
| Sidebar (mobile) | `Drawer` variant `temporary` opened from `AppBar`'s `IconButton` |
| Header bar | `AppBar` + `Toolbar` |
| Header search | `Autocomplete` (`freeSolo`, controlled) with smart-recognition logic |
| Alert badge in header | `Badge` overlapping an `IconButton` |
| Theme toggle | `IconButton` flipping `ThemeProvider` mode in MobX store |
| Density toggle | `IconButton` flipping the MUI `density` token in the theme (DataGrid `density` + custom `--row-height` token for non-grid rows) |
| Stale-per-widget overlay | `Box` with `sx={{ opacity: 0.4, pointerEvents: 'none' }}` over the widget body; corner `Chip` "stale" |
| Toast | `Snackbar` + `Alert` (auto-hide 8s; severity colour matches the alert) |
| Confirmation modal (Force rebroadcast) | `Dialog` + `DialogActions` |
| Card containers | `Card` + `CardHeader` + `CardContent` |
| Status / severity chip | `Chip` with `color` mapped from theme `palette.error/warning/success/info` |
| KPI / metric value | `Typography variant="h3"` over `Typography variant="caption"` label |
| Sparklines | `LineChart` from `@mui/x-charts` in compact mode (no axis labels) |
| Full charts (Source Metrics) | `LineChart` / `BarChart` from `@mui/x-charts` with toolbar + zoom |
| Tables (peers, alerts journal, recent broadcasts) | `DataGrid` from `@mui/x-data-grid` — sortable, filterable, density-aware |
| Vertical timeline (Tx / Address / Token detail) | `Stepper` orientation `vertical` + custom `StepIcon` per state + `StepContent` for metadata |
| Kanban (Broadcast Queue) | Three `Card` columns with `Stack` of `Card`-cards; framer-motion `AnimatePresence` for the move animation |
| Activity stream | `List` + `ListItem` with `ListItemAvatar` (source icon) + `ListItemText` (primary/secondary) |
| Tab navigation (where used) | `Tabs` + `Tab` |
| Recent-lookup chips | `Chip` row inside a `Stack direction="row"` |
| Connection status indicator | small `Chip` with status colour in `Toolbar` right side |

### Theme tokens (designer ships these)

The brief expects design tokens that map 1:1 onto an MUI
`createTheme({...})` config:

- **`palette.mode`** — `'light' | 'dark'` with toggle
- **`palette.primary` / `secondary`** — brand colours
- **`palette.severity`** — extended scale beyond MUI defaults:
  `info` / `success` / `warning` / `error` for alert
  severity; a custom `palette.score` gradient (red → amber →
  green) for the 0-100 peer score
- **`palette.background`** — `default` + `paper`
- **`typography`** — full MUI typography scale (`h1`-`h6`,
  `body1`, `body2`, `caption`, `overline`, plus a `code` for
  hashes / hex)
- **`shape.borderRadius`** — single value
- **`spacing`** — 8px MUI default unless overridden
- **`components.MuiDataGrid.defaultProps.density`** —
  switchable
- **`zIndex`** — stick with MUI defaults

The designer must NOT design custom CSS-only flourishes that
can't be expressed via `sx` / theme overrides — engineering
discipline rule from `01-stack-profile.md`: "UI layer:
MUI-only components, styling via `sx`/theme overrides. No
CSS/SCSS files for feature styling."

### Density

Dense ↔ Comfortable toggle is implemented as:
- `DataGrid` `density` prop flips between `'compact'` and
  `'standard'`.
- Non-grid rows: a custom theme token (e.g. `spacing.row`)
  flips between two values; cards / lists honour it via `sx`.

### Theme

Both light + dark, toggle in header, default = system
preference → dark fallback. Designer ships both palettes; the
toggle persists in MobX store via `mobx-persist-store`.

### Devices

Desktop primary (`md` and up: 900px+ MUI default; design at
1440px reference, layout-test at 1920px). Mobile responsive
via MUI `Drawer` swap + `Stack` reorder; **tablet UX is out
of scope** per `01-stack-profile.md`.

### Iconography

`@mui/icons-material` — the designer picks from this set; do
not commission custom icons. Code/data icons that aren't in
MUI Material Icons should use Lucide as a fallback (engineer
will wire if needed), but prefer MUI.

### Typography

MUI default = **Roboto**. Designer can override the
typography scale via `createTheme({ typography: { fontFamily }
})` but should ship a justifying reason if deviating from
Roboto.

### Accessibility

WCAG AA contrast in both palettes; keyboard navigation
(MUI gives this for free if you stick to primitives);
`aria-label` on every icon-only button.

### Animations

`framer-motion` only. Kanban card moves, timeline-stage
progression, toast slide-in. No CSS keyframes.

## 9. Deliverable from design agent

The design agent ships **MUI-native** mockups — every screen
is composed of MUI primitives listed in §8 above. Do not draw
custom buttons / inputs / dialogs / tables that don't map onto
an MUI component.

**Hi-fi Figma mockups (5 screens):**

1. **Dashboard** — 4 frames: (a) light + comfortable density,
   (b) dark + comfortable, (c) dark + dense, (d) mobile
   (375px viewport with drawer collapsed).
2. **Transaction detail** with `Stepper`-based vertical
   timeline — dark + comfortable.
3. **Broadcast Queue** kanban using 3 `Card` columns +
   framer-motion move semantics — dark + comfortable. Include
   one stale-highlight card (>5 min in Dispatching).
4. **Alerts** (active section using `Card` stack + history
   journal using `DataGrid`) — dark + comfortable.
5. **P2P Pool** (`DataGrid` of peers with inline score-component
   mini-bars + `@mui/x-charts` donut for subnet/24 diversity)
   — dark + comfortable. Show how the two visuals share the
   page.

Each frame must be drawn with **real MUI component sizing**
(`Toolbar` height 64px desktop / 56px mobile, `Drawer` width
240px, `DataGrid` row 36px compact / 52px standard,
`spacing(1)` = 8px). The implementer should be able to read
the frame and map every element back to a concrete `<Card>`
/ `<DataGrid>` / `<Snackbar>` / etc.

**Design tokens (Figma → MUI theme transferable):**

The token list must be ready to drop into a
`createTheme({...})` call. Required:

- `palette.mode = light | dark` (both palettes)
- `palette.primary` + `palette.secondary`
- Extended `palette.severity.{info|success|warning|error}`
  matched to alert severity
- Custom `palette.score` gradient (5 stops for the 0-100
  score scale on P2P Pool)
- `palette.background.{default,paper}`
- `palette.text.{primary,secondary,disabled}`
- `typography.fontFamily` (Roboto unless justified
  deviation)
- Full `typography.{h1..h6,body1,body2,caption,overline}`
  scale + a custom `typography.code` for hashes / hex / config
  keys
- `shape.borderRadius`
- `spacing` (default MUI 8px unit, override if needed)
- Density rule: two values per row-height token (compact /
  standard) flipped via the user toggle

**Interaction notes (per-screen short prose):**

For each of the 5 hi-fi screens, document:

- MUI variant chosen for each interactive component (e.g.
  `Button variant="contained" color="primary"`).
- Hover / focus / active / disabled states (MUI handles most,
  but call out anything custom).
- framer-motion transitions: which element animates on what
  event (e.g. kanban card uses `layout` + `AnimatePresence`
  on state change).
- Real-time data arrival behaviour (e.g. dashboard sparkline
  receives a new tick → animates smoothly; new toast slides
  in from top-right).
- Stale-state appearance per widget (the `Box sx={{ opacity:
  0.4, pointerEvents: 'none' }}` overlay + "stale" `Chip`).

**Out of scope for design:**

- Component library code (engineering wires the MUI components)
- Per-screen mockups for screens 6-14 in §4 (engineers build
  them from tokens + the §8 MUI mapping table + the 5 hi-fi
  references)
- Brand identity / logo
- Custom CSS / non-MUI components

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

## 13. Reference docs

### Required reading (frontend engineering standards)

These are workspace-wide rulebooks every frontend project
inherits. Read before designing — they constrain what the
designer can ship.

- `01-stack-profile.md` — default frontend baseline: React 19
  + Vite 7 + MUI + MUI X + MobX + framer-motion. The §"Default
  Frontend Baseline" section defines the stack and
  architectural rules.
- `02-frontend-principles.md` — universal engineering
  principles (layered architecture, MobX-owned business logic,
  MUI-only UI, route-driven hydration).

### Domain context (optional deep-dive)

- [`docs/runbook.md`](../../runbook.md) — operator handbook
  (deploy / monitor / rotate / recover). Replaces the
  wave-6 stub that previously lived at `03-prod-runbook.md`.
- `04-broadcast-contract.md` — broadcast contract + state
  machine for the Tx timeline.

---

End of brief. Designer: ship the 5 hi-fi mockups + tokens +
interaction notes. Engineering implements the rest from
tokens + patterns.
