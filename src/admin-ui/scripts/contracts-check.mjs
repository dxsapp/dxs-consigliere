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
import { existsSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from "node:fs";
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

// S1-audit M1: `process.exit()` inside `try { }` SKIPS the finally
// cleanup (Node tears the process down before unwinding). The
// previous version leaked `consigliere-contracts-*` temp dirs on
// every CI run. Set `process.exitCode` instead + `return` from the
// IIFE so the finally fires and the tmp dir is removed.
try {
  await runCheck();
} finally {
  rmSync(tmp, { recursive: true, force: true });
}

async function runCheck() {
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
    process.exitCode = dotnet.status ?? 1;
    return;
  }

  const pnpmExec = process.env.npm_execpath ?? "pnpm";
  const codegen = spawnSync(
    pnpmExec,
    ["exec", "openapi-typescript", freshSwagger, "-o", freshTypes],
    { stdio: "inherit", cwd: adminUiRoot }
  );
  if (codegen.status !== 0) {
    console.error("[contracts:check] openapi-typescript failed");
    process.exitCode = codegen.status ?? 1;
    return;
  }

  const swaggerDiff = diff(committedSwagger, freshSwagger, "swagger.json");
  const typesDiff = diff(committedTypes, freshTypes, "api.generated.ts");

  if (swaggerDiff === null && typesDiff === null) {
    console.log("[contracts:check] OK — committed contracts match the live backend.");
    return;
  }

  console.error("[contracts:check] FAIL — committed contracts have drifted from the backend.\n");
  if (swaggerDiff) {
    console.error("---- contracts/swagger.json drift ----");
    console.error(swaggerDiff);
    console.error("");
  }
  if (typesDiff) {
    console.error("---- src/types/api.generated.ts drift ----");
    console.error(typesDiff);
    console.error("");
  }
  console.error("Run `pnpm contracts:generate` and commit the result.");
  process.exitCode = 1;
}

/**
 * S1-audit L2: shell out to `git diff --no-index` for a real
 * unified diff. A one-line insertion no longer cascades into a
 * shifted line-by-line waterfall — git's LCS algorithm produces a
 * tight hunk. Falls back to a structural mismatch report if git is
 * absent (e.g. minimal Docker images without git installed).
 */
function diff(committedPath, freshPath, label) {
  const committed = readFileSync(committedPath, "utf-8");
  const fresh = readFileSync(freshPath, "utf-8");
  if (committed === fresh) return null;

  // `git diff --no-index` exits 1 when files differ — we expect
  // that. `--unified=3` gives 3 lines of context per hunk.
  const result = spawnSync(
    "git",
    [
      "--no-pager",
      "diff",
      "--no-index",
      "--no-color",
      "--unified=3",
      committedPath,
      freshPath,
    ],
    { stdio: ["ignore", "pipe", "pipe"], encoding: "utf-8" }
  );
  if (result.error) {
    return `[diff] git unavailable (${result.error.code}); falling back to size summary: committed=${committed.length} bytes, fresh=${fresh.length} bytes (${label})`;
  }
  return trimDiff(result.stdout || result.stderr || "");
}

function trimDiff(s) {
  const lines = s.split("\n");
  if (lines.length <= 120) return s;
  return `${lines.slice(0, 120).join("\n")}\n… (${lines.length - 120} more diff lines)`;
}
