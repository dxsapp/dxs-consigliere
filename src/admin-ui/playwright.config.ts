import { defineConfig, devices } from "@playwright/test";

/**
 * S12 — Playwright e2e config.
 *
 * Specs live in `tests/e2e/` and run against the Vite dev server in
 * mock mode (`VITE_API_MODE=mock`) so we don't need a backend host.
 * CI invokes `pnpm test:e2e`; locally you can also run
 * `pnpm playwright test --ui` for the interactive runner.
 */
export default defineConfig({
  testDir: "tests/e2e",
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  reporter: process.env.CI ? "github" : "list",
  use: {
    baseURL: process.env.E2E_BASE_URL ?? "http://127.0.0.1:5173",
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
    command: "pnpm dev",
    url: process.env.E2E_BASE_URL ?? "http://127.0.0.1:5173",
    reuseExistingServer: !process.env.CI,
    env: {
      VITE_API_MODE: "mock",
    },
    timeout: 60_000,
  },
});
