#!/usr/bin/env node
/**
 * wave-A2 S1 — regenerate `src/types/api.generated.ts` from the
 * canonical Swashbuckle document.
 *
 * Pipeline:
 *   1. `dotnet run --project ../Dxs.Consigliere -- --emit-swagger <out>`
 *      writes the v1 OpenAPI spec to `contracts/swagger.json`.
 *   2. `openapi-typescript` reads it + writes the TS file.
 *
 * `pnpm contracts:check` re-runs this into a tmp dir + diffs.
 */
import { spawnSync } from "node:child_process";
import { existsSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const adminUiRoot = resolve(here, "..");
const backendProject = resolve(adminUiRoot, "..", "Dxs.Consigliere", "Dxs.Consigliere.csproj");
const swaggerOut = process.env.SWAGGER_OUT ?? resolve(adminUiRoot, "contracts", "swagger.json");
const typesOut = process.env.TYPES_OUT ?? resolve(adminUiRoot, "src", "types", "api.generated.ts");
const configuration = process.env.SWAGGER_DOTNET_CONFIG ?? "Release";

if (!existsSync(backendProject)) {
  console.error(`[contracts:generate] backend project not found: ${backendProject}`);
  process.exit(2);
}

console.log(`[contracts:generate] dotnet run -- --emit-swagger ${swaggerOut}`);
const dotnet = spawnSync(
  "dotnet",
  [
    "run",
    "--project",
    backendProject,
    "-c",
    configuration,
    "-p:SkipAdminUiBuild=true",
    "--",
    "--emit-swagger",
    swaggerOut,
  ],
  { stdio: "inherit" }
);
if (dotnet.status !== 0) {
  console.error("[contracts:generate] swagger emit failed");
  process.exit(dotnet.status ?? 1);
}

console.log(`[contracts:generate] openapi-typescript ${swaggerOut} → ${typesOut}`);
const pnpmExec = process.env.npm_execpath ?? "pnpm";
const codegen = spawnSync(
  pnpmExec,
  ["exec", "openapi-typescript", swaggerOut, "-o", typesOut],
  { stdio: "inherit", cwd: adminUiRoot }
);
if (codegen.status !== 0) {
  console.error("[contracts:generate] openapi-typescript failed");
  process.exit(codegen.status ?? 1);
}

console.log("[contracts:generate] OK");
