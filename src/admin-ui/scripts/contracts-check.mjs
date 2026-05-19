#!/usr/bin/env node
/**
 * wave-A2 S1 — CI drift gate. Regenerates the OpenAPI spec + the
 * TS types into a temp dir and diffs them against the committed
 * copies. Exits 1 on any mismatch.
 *
 * The script never mutates the working tree. A failing run prints
 * the unified diff so an operator can run
 * `pnpm contracts:generate` + commit the result.
 */
import { spawnSync } from "node:child_process";
import { existsSync, mkdtempSync, readFileSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const adminUiRoot = resolve(here, "..");
const backendProject = resolve(adminUiRoot, "..", "Dxs.Consigliere", "Dxs.Consigliere.csproj");
const committedSwagger = resolve(adminUiRoot, "contracts", "swagger.json");
const committedTypes = resolve(adminUiRoot, "src", "types", "api.generated.ts");
const configuration = process.env.SWAGGER_DOTNET_CONFIG ?? "Release";

if (!existsSync(committedSwagger)) {
  console.error(`[contracts:check] missing committed snapshot: ${committedSwagger}`);
  process.exit(2);
}
if (!existsSync(committedTypes)) {
  console.error(`[contracts:check] missing committed types: ${committedTypes}`);
  process.exit(2);
}
if (!existsSync(backendProject)) {
  console.error(`[contracts:check] backend project not found: ${backendProject}`);
  process.exit(2);
}

const tmp = mkdtempSync(join(tmpdir(), "consigliere-contracts-"));
const freshSwagger = join(tmp, "swagger.json");
const freshTypes = join(tmp, "api.generated.ts");

try {
  console.log("[contracts:check] regenerating into a temp dir…");
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
      freshSwagger,
    ],
    { stdio: "inherit" }
  );
  if (dotnet.status !== 0) {
    console.error("[contracts:check] swagger emit failed");
    process.exit(dotnet.status ?? 1);
  }

  const pnpmExec = process.env.npm_execpath ?? "pnpm";
  const codegen = spawnSync(
    pnpmExec,
    ["exec", "openapi-typescript", freshSwagger, "-o", freshTypes],
    { stdio: "inherit", cwd: adminUiRoot }
  );
  if (codegen.status !== 0) {
    console.error("[contracts:check] openapi-typescript failed");
    process.exit(codegen.status ?? 1);
  }

  const diffSwagger = diff(readFileSync(committedSwagger, "utf-8"), readFileSync(freshSwagger, "utf-8"));
  const diffTypes = diff(readFileSync(committedTypes, "utf-8"), readFileSync(freshTypes, "utf-8"));

  if (diffSwagger === null && diffTypes === null) {
    console.log("[contracts:check] OK — committed contracts match the live backend.");
    process.exit(0);
  }

  console.error("[contracts:check] FAIL — committed contracts have drifted from the backend.\n");
  if (diffSwagger) {
    console.error("---- contracts/swagger.json drift ----");
    console.error(trimDiff(diffSwagger));
    console.error("");
  }
  if (diffTypes) {
    console.error("---- src/types/api.generated.ts drift ----");
    console.error(trimDiff(diffTypes));
    console.error("");
  }
  console.error("Run `pnpm contracts:generate` and commit the result.");
  process.exit(1);
} finally {
  rmSync(tmp, { recursive: true, force: true });
}

/** Returns null when identical; otherwise a tiny unified-style diff. */
function diff(committed, fresh) {
  if (committed === fresh) return null;
  const a = committed.split(/\r?\n/);
  const b = fresh.split(/\r?\n/);
  const out = [];
  const max = Math.max(a.length, b.length);
  let printed = 0;
  for (let i = 0; i < max && printed < 60; i++) {
    if (a[i] !== b[i]) {
      out.push(`@${i + 1}`);
      if (a[i] !== undefined) out.push(`- ${a[i]}`);
      if (b[i] !== undefined) out.push(`+ ${b[i]}`);
      printed += 1;
    }
  }
  return out.join("\n");
}

function trimDiff(s) {
  const lines = s.split("\n");
  if (lines.length <= 60) return s;
  return `${lines.slice(0, 60).join("\n")}\n… (${lines.length - 60} more lines)`;
}
