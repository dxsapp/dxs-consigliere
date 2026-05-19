import { defineConfig } from "vitest/config";
import path from "node:path";

// Contract-parity tests (wave-A2 S2). Boots a live ASP.NET host
// (a single instance shared across every spec inside one fork)
// and validates real endpoint payloads against the schemas in
// contracts/swagger.json via ajv. Kept separate from unit tests
// so CI can decide when to run it (it's heavier).
export default defineConfig({
  resolve: {
    alias: {
      "@": path.resolve(__dirname, "./src"),
    },
  },
  test: {
    environment: "node",
    include: ["tests/contract/**/*.test.ts"],
    // S2-audit H1: globalSetup spawns the backend host in the
    // MAIN process so teardown is reliable. The previous
    // process.on("beforeExit") path never fired because the
    // live dotnet child kept the event loop active.
    globalSetup: ["./tests/contract/_global-setup.ts"],
    testTimeout: 90_000,
    hookTimeout: 180_000,
    pool: "forks",
    poolOptions: {
      forks: {
        // One worker — every spec shares the SAME running host
        // via `inject("contractHostBaseUrl")`. Parallel forks
        // would race `/api/setup/complete` (only succeeds once
        // per DB).
        singleFork: true,
      },
    },
  },
});
