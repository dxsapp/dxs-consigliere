import { defineConfig, devices } from "@playwright/test";

/**
 * S12 — Playwright e2e config.
 *
 * Specs live in `tests/e2e/` and run against the Vite dev server in
 * mock mode (`VITE_API_MODE=mock`) so we don't need a backend host.
 * CI invokes `pnpm test:e2e`; locally you can also run
 * `pnpm playwright test --ui` for the interactive runner.
 *
 * S7-S12-audit M3: Vite's default bind is the loopback `localhost`;
 * the webServer is now explicitly pinned to `--host 127.0.0.1` so
 * both projects, the webServer waiter, and the `use.baseURL` agree
 * on a single host string.
 */
const HOST = "127.0.0.1";
const PORT = 5173;
const BASE_URL = process.env.E2E_BASE_URL ?? `http://${HOST}:${PORT}`;

export default defineConfig({
  testDir: "tests/e2e",
  // S7-S12-audit M3 followup: serialize specs against the single
  // shared Vite dev server. Parallel specs raced the cold-mounting
  // SPA + the shared MockAuthClient localStorage; one-worker
  // execution is deterministic on the same wall-clock as the full
  // suite (~25s) so we trade nothing for the stability.
  fullyParallel: false,
  workers: 1,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  reporter: process.env.CI ? "github" : "list",
  use: {
    baseURL: BASE_URL,
    trace: "on-first-retry",
  },
  projects: [
    { name: "chromium", use: { ...devices["Desktop Chrome"] } },
    {
      name: "mobile-chromium",
      use: { ...devices["Pixel 5"] },
    },
  ],
  webServer: {
    command: `pnpm dev --host ${HOST} --port ${PORT} --strictPort`,
    url: BASE_URL,
    reuseExistingServer: !process.env.CI,
    env: {
      VITE_API_MODE: "mock",
    },
    timeout: 60_000,
  },
});
