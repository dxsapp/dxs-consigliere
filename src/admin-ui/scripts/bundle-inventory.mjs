#!/usr/bin/env node
/**
 * S11 — per-route bundle inventory. Reads every JS chunk in
 * `wwwroot/assets`, gzip-compresses it, and prints a markdown table
 * the closeout doc (S12) can paste verbatim.
 *
 * The shell + per-route ceilings live as named env overrides:
 *   SHELL_BUDGET_BYTES_GZIP   — default 204800 (A1 M3, 200 KB)
 *   ROUTE_BUDGET_BYTES_GZIP   — default 256000 (250 KB per-route)
 *
 * Exits 1 when ANY chunk breaches its ceiling — CI gate.
 */
import { gzipSync } from "node:zlib";
import { readdirSync, readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const assetsDir = join(here, "..", "..", "Dxs.Consigliere", "wwwroot", "assets");
const shellCap = Number.parseInt(process.env.SHELL_BUDGET_BYTES_GZIP ?? "204800", 10);
const routeCap = Number.parseInt(process.env.ROUTE_BUDGET_BYTES_GZIP ?? "256000", 10);

const files = readdirSync(assetsDir)
  .filter((n) => n.endsWith(".js"))
  .map((n) => {
    const buf = readFileSync(join(assetsDir, n));
    return { name: n, raw: buf.length, gzip: gzipSync(buf).length };
  })
  .sort((a, b) => b.gzip - a.gzip);

// Shell = the largest `index-*.js` (vite calls the cold-load entry
// `index-<hash>.js`; SignalR's tiny stub is the same prefix).
const shell = files
  .filter((f) => /^index-[A-Za-z0-9_-]+\.js$/.test(f.name))
  .sort((a, b) => b.raw - a.raw)[0];

let failed = false;

console.log("| chunk | raw KB | gzip KB | budget | status |");
console.log("|---|---:|---:|---:|---|");
for (const f of files) {
  const isShell = shell && f.name === shell.name;
  const cap = isShell ? shellCap : routeCap;
  const ok = f.gzip <= cap;
  if (!ok) failed = true;
  const tag = isShell ? "shell" : "route";
  console.log(
    `| ${f.name} | ${(f.raw / 1024).toFixed(2)} | ${(f.gzip / 1024).toFixed(2)} | ` +
      `${(cap / 1024).toFixed(0)} (${tag}) | ${ok ? "OK" : "FAIL"} |`
  );
}

if (failed) {
  console.error("[inventory] one or more chunks exceeded their budget");
  process.exit(1);
}
