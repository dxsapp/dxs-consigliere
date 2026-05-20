# Consigliere Admin UI — Design Handoff Bundle

Self-contained package for the design agent. All references
inside `00-design-brief.md` resolve to sibling files in this
folder.

## Reading order

1. **`00-design-brief.md`** — primary brief. Read end-to-end
   first. Contains the IA, screen catalog, MUI mapping table,
   design-token requirements, and deliverable list.

2. **`01-stack-profile.md`** — workspace-default frontend
   stack baseline (MUI + MUI X + MobX + framer-motion).
   Mandatory constraints, not suggestions.

3. **`02-frontend-principles.md`** — universal frontend
   engineering principles the implementation will follow.
   Affects what the design must enable (layered architecture,
   MobX-owned state, MUI-only UI).

4. **`docs/runbook.md`** (repo root → `docs/runbook.md`) —
   operator handbook for production deployments. Replaces
   the wave-6 design-handoff stub that used to live at
   `03-prod-runbook.md`; the new runbook is the single
   source of truth for ops topics (deploy / monitor /
   rotate / recover).

5. **`04-broadcast-contract.md`** — domain context: broadcast
   subsystem state machine (Validated → Dispatching →
   PeerRelayed → Mined → Confirmed). Drives the Tx timeline
   on the Transaction detail screen.

## Deliverable

Per `00-design-brief.md` §9: 5 hi-fi Figma mockups (Dashboard
× 4 variants, Transaction detail, Broadcast Queue, Alerts,
P2P Pool) + design tokens shaped as a MUI `createTheme({...})`
config + per-screen interaction notes.

## Stack the mockups must respect

- React 19 + Vite 7 + TypeScript
- MUI (`@mui/material`, `@mui/icons-material`)
- MUI X (`@mui/x-data-grid`, `@mui/x-charts`)
- MobX + `mobx-persist-store`
- `framer-motion`
- Desktop primary (1440/1920) + mobile responsive; tablet out
  of scope.
