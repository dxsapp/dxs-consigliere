#!/usr/bin/env node
/**
 * S6-audit M2 — explicit shell budget assertion.
 *
 * Reads the latest `wwwroot/assets/index-*.js` (the cold-load shell)
 * after `pnpm build`, gzip-compresses it, and fails the build if the
 * gzipped size exceeds the A1 M3 200 KB ceiling.
 *
 * Lazy chunks (Dashboard, BroadcastQueuePage, framer-motion, etc.)
 * are NOT included in the shell budget — they have their own
 * per-route budget tracked in the master ledger.
 *
 * Override the cap with SHELL_BUDGET_BYTES_GZIP (used by audits that
 * need to bump the ceiling temporarily).
 */
import { gzipSync } from "node:zlib";
import { readdirSync, readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const assetsDir = join(here, "..", "..", "Dxs.Consigliere", "wwwroot", "assets");
const cap = Number.parseInt(process.env.SHELL_BUDGET_BYTES_GZIP ?? "204800", 10);

const candidates = readdirSync(assetsDir).filter(
  (n) => /^index-[A-Za-z0-9_-]+\.js$/.test(n)
);
if (candidates.length === 0) {
  console.error(`[budget] no shell chunk in ${assetsDir} — run \`pnpm build\` first.`);
  process.exit(2);
}
// Of the matching `index-*.js`, the shell is the LARGEST one (the
// other small `index-*.js` is the SignalR dynamic-import chunk).
const shell = candidates
  .map((n) => {
    const buf = readFileSync(join(assetsDir, n));
    return { name: n, raw: buf.length, gzip: gzipSync(buf).length };
  })
  .sort((a, b) => b.raw - a.raw)[0];

const ok = shell.gzip <= cap;
const status = ok ? "OK" : "FAIL";
const ratio = (shell.gzip / cap) * 100;
console.log(
  `[budget] ${status}: shell ${shell.name} = ${(shell.gzip / 1024).toFixed(2)} KB gzip ` +
    `(cap ${(cap / 1024).toFixed(0)} KB · ${ratio.toFixed(1)}% used)`
);
if (!ok) {
  console.error(
    `[budget] shell exceeds the A1 M3 200 KB ceiling by ${(
      (shell.gzip - cap) /
      1024
    ).toFixed(2)} KB. Push code to a lazy route chunk OR bump SHELL_BUDGET_BYTES_GZIP with an audited rationale.`
  );
  process.exit(1);
}
