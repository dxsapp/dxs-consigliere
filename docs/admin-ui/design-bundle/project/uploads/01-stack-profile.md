# Project Stack Profiles (Agent Baseline)

## Purpose
Cross-project registry of concrete stack choices and non-negotiable implementation rules.
Use together with `frontend-principles.md`:
- `frontend-principles.md` = universal engineering rules.
- `project-stack-profiles.md` = stack baselines and execution constraints that apply across our projects.

## How agents should use this file
1. Start from the default baseline profile below for any frontend project in our workspace.
2. Apply listed stack/rules as hard constraints unless a project has an explicit override.
3. If project stack/rules change globally, update this file in the same PR.
4. If one project needs a justified deviation, add a separate project-specific override section.

---

## Profile: Default Frontend Baseline (All Projects)

- Scope: all frontend projects by default
- Note: this profile was originally documented under Huep Admin Web, but is now the shared baseline for all our frontend repos.

### Stack
- React 19 + TypeScript + Vite 7
- MUI (`@mui/material`, `@mui/icons-material`)
- MUI Data Grid (`@mui/x-data-grid`)
- MUI X Charts (`@mui/x-charts`)
- MobX (`mobx`, `mobx-react-lite`)
- Persistence: `mobx-persist-store`
- Routing: `react-router-dom` 7
- Animations: `framer-motion`
- Package manager: `pnpm`

### API and contract
- Backend API is the only source of integration (no direct DB access).
- Each project must declare an explicit contract source of truth (for example: OpenAPI file, generated SDK, shared schema package, or backend contract repo).
- DTOs and frontend domain types must stay aligned with the project contract source.
- API calls go through the project's shared client/service layer, not direct page-level `fetch`.
- Project-specific paths such as `openapi/*.json`, `src/types/api.ts`, or `src/lib/api.ts` are examples, not mandatory default locations.

### UI and architecture constraints
- Business logic and async flows live in MobX stores.
- UI layer: MUI-only components, styling via `sx`/theme overrides.
- No CSS/SCSS files for feature styling.
- For UI animations/transitions, use `framer-motion`.
- Tables: MUI DataGrid only.
- Target form factors: desktop + mobile (tablet UX is out of scope).
- RBAC must be enforced in UX (hide/disable unavailable actions), while backend remains authority.

### Delivery and quality gates
- Required local gates before merge:
  - `pnpm typecheck`
  - `pnpm lint`
  - `pnpm build`
  - `pnpm test`
  - `pnpm verify`
- API mode switch (`real`/`mock`) is env-driven; mock should mirror real contract shape.
- If architecture, hydration, contracts, or operational behavior change, update the corresponding docs in the same change set or explicitly mark them stale.

## Project-specific overrides

Add a separate section only when a specific repo must intentionally diverge from the default baseline above.

### Override: X4Padel Mobile (React Native)

- Scope: `apps/mobile` in the X4Padel monorepo.
- Reason for override: the default baseline targets web (Vite + MUI). Mobile requires a React Native stack; universal principles still apply via `frontend-react-native-addendum.md`.

#### Stack
- React Native with Expo Prebuild + Custom Dev Client (not Managed, not bare)
- TypeScript (strict)
- React Navigation 7 (native stack + bottom tabs)
- MobX + `mobx-react-lite` + `mobx-persist-store`
- Persistence adapter: `react-native-mmkv`
- Secrets/tokens: `expo-secure-store` (Keychain/Keystore)
- HTTP client: `openapi-fetch` against a shared OpenAPI 3.1 contract package
- UI: React Native Paper + local `packages/ui-native` primitives + `packages/tokens`
- Safe areas: `react-native-safe-area-context`
- Lists: `@shopify/flash-list`
- Animations: `react-native-reanimated` v3 + `moti` + `react-native-gesture-handler`
- Forms: `react-hook-form` + `zod`
- Maps: `react-native-yamap` (Yandex MapKit wrapper; requires Prebuild)
- Device: `expo-notifications`, `expo-linking`, `expo-camera`, `expo-image-picker`, `expo-location`, `expo-image`
- Observability: Sentry RN + Amplitude
- Testing: Jest + React Native Testing Library (unit), Maestro (e2e)
- Build/release: EAS Build + Expo Updates (OTA), channels: `development` / `preview` / `production`
- Package manager: `pnpm` workspace + Turborepo

#### Rules
- Tokens and secrets must live in `expo-secure-store`, never in `MMKV` / `AsyncStorage`.
- OTA updates may ship JS-only changes. Any native module, permission, entitlement, or `app.config.ts` change requires a full EAS Build.
- Route-driven hydration pattern follows `frontend-react-native-addendum.md` section "Route-Driven Hydration in React Native" (React Navigation `useFocusEffect` / shell listeners over page `useEffect`).
- Maps and any other native-dependent module must be listed in the project's `docs/native-modules.md` registry.
- Web Club Dashboard for this project (`apps/club-dashboard`) follows the default baseline above without override.

See `frontend-react-native-addendum.md` for the full mapping of universal principles to React Native and for RN-specific audit adaptations.
