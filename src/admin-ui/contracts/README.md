# Backend contract source (A1 H4)

This directory hosts the Consigliere admin REST contract that the
TypeScript DTOs in `src/types/` mirror. Required by Core Rule §7.

## Current state (S3)

The backend does not yet emit a checked-in Swagger JSON file; the
Swashbuckle pipeline produces it at runtime under `/swagger/v1/swagger.json`.
For S3 we ship hand-written TS DTOs in `src/types/auth.ts` (and
later under `src/types/api.generated.ts` once the codegen lands) and
treat this folder as the snapshot target.

## Plan (S3 followup / S9 hardening)

1. Add a backend step that exports `swagger.json` to this folder
   during `dotnet build` (or a one-shot `dotnet run --project
   ... -- --emit-swagger` task).
2. Wire `openapi-typescript` (or equivalent) into `package.json` so
   `pnpm contracts:generate` regenerates `src/types/api.generated.ts`
   from `contracts/swagger.json`.
3. The Vitest `test:contract` config (already shipped in
   `vitest.contract.config.ts`) boots the ASP.NET host and validates
   real endpoint payloads against the generated TS types.

For now, the hand-written `src/types/auth.ts` matches the C# DTOs at
`src/Dxs.Consigliere/Dto/{Requests,Responses}/AdminAuth*.cs`
verbatim. Any backend rename triggers a TypeScript compile error
at the call site — a manual but reliable parity check.
