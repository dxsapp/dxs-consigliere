# Slices

## `AA1` Admin API Inventory
- enumerate current admin and ops endpoints
- identify DTOs already suitable for UI
- identify missing endpoints for details/dashboard/auth shell
- avoid adding UI-only backend duplication if existing endpoints are enough

## `AA2` Simple Admin Auth Backend
- config model for admin auth
- configurable single username
- password hash support
- auth-disable switch for trusted deployments
- cookie-based login/logout/me endpoints
- admin route protection
- startup/config validation
- narrow tests for login/logout/guard behavior

## `AA3` Claude Handoff Package
- product spec for screens/actions/states
- API map with exact endpoints and DTO shapes
- stack/rules file that binds Claude to `/Users/imighty/Code/docs` frontend baseline
- explicitly state route-driven hydration and MobX ownership rules

## `AA4` Frontend Admin Shell
- implemented by Claude against the handoff package
- should stay thin over API/store contracts
- no business logic duplicated into UI components

## `AA5` Verification
- login flow
- tracked scope create/remove flows
- full-history upgrade flows
- rooted token security visibility
- runtime/storage/source status visibility
