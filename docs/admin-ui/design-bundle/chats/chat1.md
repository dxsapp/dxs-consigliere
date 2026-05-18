# Consigliere Admin UI

_Started 2026-05-18 13:14 UTC_

---

## User

Design the admin UI for Consigliere — a BSV thin-node observer.
Engineering ships on the workspace-default frontend stack:
React 19 + Vite 7 + MUI (@mui/material + @mui/icons-material
+ @mui/x-data-grid + @mui/x-charts) + MobX + framer-motion.
Mockups must be MUI-native — every element maps onto an MUI
primitive listed in the brief; no custom buttons / tables /
dialogs.

I'm attaching 6 files (read in this order):

  README.md                     — index + reading order
  00-design-brief.md            — PRIMARY: read end-to-end first
  01-stack-profile.md           — MANDATORY stack constraints
  02-frontend-principles.md     — MANDATORY engineering principles
  03-prod-runbook.md            — optional domain context
  04-broadcast-contract.md      — optional state-machine context

Deliverable per 00-design-brief.md §9:
  - 5 hi-fi Figma mockups: Dashboard (× 4 variants:
    light+comfortable, dark+comfortable, dark+dense, mobile),
    Transaction detail, Broadcast Queue kanban, Alerts, P2P Pool.
  - Design tokens shaped as a MUI createTheme({...}) config
    (palette + typography + shape + spacing + components),
    both light + dark.
  - Per-screen interaction notes (MUI variants used,
    framer-motion transitions, stale-state appearance).

Both light + dark palettes; dense + comfortable density;
desktop primary (1440/1920) + mobile responsive; tablet out
of scope.

Start with 00-design-brief.md end-to-end — it has the screen
priority order, the MUI mapping table for every UI element,
and the exact list of frames I need. Treat 01 + 02 as hard
constraints; if you'd otherwise want to invent a component
shape that's not in MUI, flag it instead.

## Assistant

_[tool: read_file]_

_[tool: read_file]_

_[tool: read_file]_

_[tool: read_file]_

_[tool: read_file]_

_[tool: read_file]_

_[tool: list_files]_

_[tool: update_todos]_

The design system project is empty — so I'll anchor on MUI's own visual conventions (which the brief says IS the design system) and only invent the brand color choices and the score-gradient/severity overlays the brief specifies.

Let me plan and lay out the work:

_[tool: copy_starter_component]_

_[tool: read_file]_

_[tool: grep]_

_[tool: write_file]_

Got it. The design system project is empty so MUI's primitives ARE the source of truth, with brand-color and code-typography choices on top. Let me commit to a visual direction up-front and then build.

**Visual direction:**
- **Primary indigo** (`#3D5AFE` light / `#8C9EFF` dark) — observability-tool feel, distinct from BSV-orange (this isn't a wallet)
- **Secondary teal** (`#00BFA5`) for data/code accents
- **Background dark** `#0E1116` (slightly darker than MUI default `#121212`) — sharper for always-on monitors
- **Roboto** primary, **JetBrains Mono** for `typography.code` (defensible deviation per brief — UI shows lots of hex)
- **Shape borderRadius 8** (modern upgrade from MUI default 4)
- **Density**: row 52px comfortable / 36px dense; flips DataGrid `density` + custom `--row-height` token

Let me write the theme file first:

_[tool: write_file]_

Now the MUI-mimic primitives — these need to land at MUI's exact sizing (Toolbar 64px, Drawer 240px, DataGrid row 36/52, etc.) so engineers can map every element back to a concrete `<Card>` / `<DataGrid>` / etc.

_[tool: write_file]_

_[tool: write_file]_

Now the dashboard — the always-on hero screen, dark + comfortable:

_[tool: write_file]_

_[tool: write_file]_

_[tool: write_file]_

_[tool: write_file]_

_[tool: write_file]_

_[tool: write_file]_

_[tool: write_file]_

Now the HTML shell wiring everything into a design canvas:

_[tool: write_file]_

_[tool: show_html]_

_[tool: show_to_user]_

_[tool: get_webview_logs]_

_[tool: save_screenshot]_

Clean — no errors. Let me see how it renders before handing off.

_[tool: view_image]_

_[tool: save_screenshot]_

The Material Symbols icons are showing as text because the font hasn't fully loaded when the screenshot fires. Let me wait longer and re-check.

_[tool: save_screenshot]_

_[tool: view_image]_

_[tool: str_replace_edit]_

The Material Symbols class needs the full `font-family` declaration — Google's CSS only ships the `@font-face`, not the helper class. Let me fix:

_[tool: save_screenshot]_

_[tool: view_image]_

_[tool: show_html]_

_[tool: save_screenshot]_

_[tool: view_image]_

_[tool: fork_verifier_agent]_

Let me probe what's actually happening — maybe a font/style is being overridden. I'll fork a verifier to inspect:

I'll wait for the verifier's report on the icon rendering before continuing.

## User

Verifier agent check completed

## Assistant

_[tool: update_todos]_

_[tool: done]_

Verifier confirms all 12 artboards render cleanly and the icon "issue" was just a screenshot-tool artifact — Material Symbols ligatures resolve correctly in the live browser. Wrapping up:Shipped. Open the readme artboard for the index; click any frame's expand icon (top-right on hover) to view it fullscreen.

**What's in there:**

| Section | Artboards |
|---|---|
| 1 · Dashboard | dark+comfortable (canonical), light+comfortable, dark+dense, mobile 375, 1920 layout-test |
| 2 · Transaction detail | Stepper vertical · 5-stage OutgoingTxState lifecycle |
| 3 · Broadcast Queue | 3-col kanban · one stale card · Force-rebroadcast toast |
| 4 · Alerts | Active alert Cards (all 4 P2pAlertType rules) + history DataGrid |
| 5 · P2P Pool | Peers DataGrid (ScoreBar + component MiniBars) + /24 donut |
| Design tokens | Both palettes + score gradient + type scale + drop-in `createTheme({...})` config |
| Interaction notes | Per-screen MUI variants, framer-motion transitions, stale-state behaviour |

**Flagged deviations** (both defensible, called out in the readme):
- `typography.code` uses JetBrains Mono — Roboto aligns hex poorly and the UI is hex-heavy
- `shape.borderRadius: 8` (up from MUI default 4) — modern observability-tool feel

**No MUI-shaped escapes** — every visual maps onto a primitive in §8: AppBar/Toolbar, permanent Drawer, Autocomplete freeSolo, Card+CardHeader+CardContent, Chip (with `color="severity.*"` token), Stepper orientation vertical, DataGrid (compact/standard density), x-charts LineChart compact + PieChart donut, Snackbar+Alert, Dialog+DialogActions for the one destructive action. The score-component MiniBars and ScoreBar are styled `<Box>` inside DataGrid `renderCell` — not new primitives.

Next steps when you're ready: pass me a logo / brandmark if you want one in the sidebar, and I'll iterate the Tx-detail tabs (Inputs/Outputs/Projection state) into their own sub-mocks.

