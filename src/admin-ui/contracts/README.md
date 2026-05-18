# Backend contract source (A1 H4 + S3-audit L2)

This directory hosts the Consigliere admin REST contract that the
TypeScript DTOs in `src/types/` mirror. Required by Core Rule §7.

## Current state (S3)

The backend does not yet emit a checked-in Swagger JSON file; the
Swashbuckle pipeline produces it at runtime under
`/swagger/v1/swagger.json`. For S3 we ship hand-written TS DTOs in
`src/types/auth.ts` and route constants in `src/lib/api/routes.ts`,
and treat this folder as the snapshot target.

## Plan (S3 followup — concrete commands)

### Step 1 — pin backend swagger export

Add a backend MSBuild target that emits the swagger snapshot
during `dotnet build`. The target shape:

```bash
# From repo root, runs the ASP.NET host headless + writes
# swagger snapshot, then exits.
dotnet run --project src/Dxs.Consigliere -- --emit-swagger \
  > src/admin-ui/contracts/swagger.json
```

The `--emit-swagger` flag is a new `IHostApplicationLifetime`-
gated branch that boots the Swashbuckle generator, writes
`Console.WriteLine(generator.SerializeToString())`, and exits 0
before the Kestrel listener starts. Backend slice owner:
`public-api-and-realtime` zone.

### Step 2 — wire codegen

Add to `src/admin-ui/package.json` scripts:

```jsonc
{
  "scripts": {
    "contracts:generate":
      "openapi-typescript contracts/swagger.json -o src/types/api.generated.ts",
    "contracts:check":
      "openapi-typescript contracts/swagger.json -o /tmp/api.generated.ts && diff -u src/types/api.generated.ts /tmp/api.generated.ts"
  },
  "devDependencies": {
    "openapi-typescript": "^7.4.0"
  }
}
```

`contracts:check` runs in CI to fail when the committed file drifts
from the regenerated output.

### Step 3 — wire the parity test

`vitest.contract.config.ts` is already in place. The test fixture
lands at `src/admin-ui/tests/contract/auth.test.ts`:

```ts
// Spawn `dotnet run --project src/Dxs.Consigliere` against a
// random port, wait for /swagger/v1/swagger.json, fetch each
// endpoint's seed payload, and validate against the generated
// types via Ajv or zod. Tear down the host on `finally`.
```

CI invocation:

```bash
cd src/admin-ui && pnpm test:contract
```

### Until then

- `src/types/auth.ts` matches the C# DTOs at
  `src/Dxs.Consigliere/Dto/{Requests,Responses}/AdminAuth*.cs`
  verbatim. Any backend rename triggers a TypeScript compile
  error at the call site — a manual but reliable parity check.
- `src/lib/api/routes.ts` pins the SignalR hub path + admin auth
  route strings; `src/lib/api/factory.test.ts` (S3-audit H1 fix)
  asserts the route constants against the backend values.
