import { describe, it } from "vitest";

/**
 * Contract-parity test scaffold (S3-audit L2).
 *
 * The full test boots `dotnet run --project src/Dxs.Consigliere`
 * against a random port, fetches `/api/admin/auth/me` + login +
 * logout, and validates each payload against the generated TS
 * types from `contracts/swagger.json`. See `contracts/README.md`
 * for the exact command sequence.
 *
 * Skipped until the S3 followup lands the `--emit-swagger` backend
 * flag + the `pnpm contracts:generate` codegen step. Until then,
 * `src/types/auth.ts` is hand-mirrored from C# (which gives a
 * compile-time parity check at every call site).
 */
describe.skip("admin auth contract parity (S3 followup)", () => {
  it("GET /api/admin/auth/me matches AdminAuthStatusResponse", () => {
    // pending: pnpm contracts:generate + ASP.NET host boot harness
  });

  it("POST /api/admin/auth/login round-trips AdminAuthStatusResponse", () => {
    // pending
  });

  it("POST /api/admin/auth/logout flips authenticated=false", () => {
    // pending
  });
});
