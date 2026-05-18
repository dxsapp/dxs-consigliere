import { defineConfig } from "vitest/config";

// Contract-parity tests (S3 deliverable). Boots a live ASP.NET host
// and validates real endpoint payloads against the generated TS
// types from contracts/swagger.json. Kept separate from unit tests
// so CI can decide when to run it (it's slower).
export default defineConfig({
  test: {
    environment: "node",
    include: ["tests/contract/**/*.test.ts"],
    testTimeout: 60_000,
  },
});
