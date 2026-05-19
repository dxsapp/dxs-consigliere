import { describe, expect, it } from "vitest";
import { sanitize } from "./sanitizer";

// wave-A3 S4 — the sanitizer logic moved into its own module
// when the LogsPage stopped being a paste box and became a live
// tail (the backend's `LogSanitizer` is the primary; this
// regex set is the client-side defence-in-depth pass). These
// tests preserve the wave-A1 rule pins on that regex set.
describe("logs sanitizer (defense-in-depth, wave-A3 S4 client pass)", () => {
  it("redacts Authorization headers (incl. multi-token Bearer values — S7-S12-audit H3)", () => {
    const { out, stats } = sanitize("GET /x\nAuthorization: Bearer abc.def.ghi\nHost: x");
    expect(out).toMatch(/Authorization: \*\*\*/);
    // The whole bearer value must be gone, not just the first token.
    expect(out).not.toContain("abc.def.ghi");
    expect(out).not.toContain("Bearer");
    expect(stats.find((s) => s.rule.includes("Authorization"))?.count).toBe(1);
  });

  it("redacts Cookie headers", () => {
    const { out } = sanitize("Cookie: session=abc; admin=1\nbody");
    expect(out).toMatch(/Cookie: \*\*\*/);
  });

  it("redacts BSV WIF keys", () => {
    const raw = "tx KywNtkAmgXZQBYWoth5pgMj6PmTnkfaT4pGc1LVWCLyBjpr2jvcX broadcast";
    const { out, stats } = sanitize(raw);
    expect(out).toContain("***WIF***");
    expect(stats.find((s) => s.rule.includes("WIF"))?.count).toBe(1);
  });

  it("redacts apiKey fields in JSON (double-quoted)", () => {
    const raw = '{"apiKey":"sk_live_abcdef","other":"keep"}';
    const { out } = sanitize(raw);
    expect(out).toMatch(/"apiKey":\s*"\*\*\*"/);
    expect(out).not.toContain("sk_live_abcdef");
    expect(out).toContain('"other":"keep"');
  });

  it("redacts apiKey fields in single-quoted log fragments (S7-S12-audit H3)", () => {
    const raw = "{'apiKey':'abcdef','other':'keep'}";
    const { out } = sanitize(raw);
    expect(out).toMatch(/'apiKey':\s*'\*\*\*'/);
    expect(out).not.toContain("abcdef");
    expect(out).toContain("'other':'keep'");
  });

  it("redacts api_key with underscore + the = assignment form", () => {
    const { out } = sanitize('api_key="snake_abc"  legacy=ok');
    expect(out).not.toContain("snake_abc");
    expect(out).toContain("legacy=ok");
  });

  it("truncates long hex blobs to first/last 12 chars", () => {
    const hex = "a".repeat(160);
    const { out } = sanitize(`raw=${hex} done`);
    expect(out).toContain("…");
    expect(out.length).toBeLessThan(160);
  });

  it("returns no stats when nothing matched", () => {
    const { out, stats } = sanitize("hello world");
    expect(out).toBe("hello world");
    expect(stats.length).toBe(0);
  });
});
