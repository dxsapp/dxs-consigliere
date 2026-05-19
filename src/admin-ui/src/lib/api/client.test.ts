import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { ApiClient } from "./client";

/**
 * wave-A3 S1 — pins the single-retry contract for 429
 * responses. The backend emits Retry-After on every limiter
 * rejection (wave-A3 S1 `OnRejectedAsync`), so the client
 * honours it ONCE; a second 429 surfaces as
 * `AppError { category: "RateLimited" }` so the UI can render
 * a calm-down banner instead of looping.
 */
describe("ApiClient 429 handling", () => {
  let originalFetch: typeof globalThis.fetch;
  const fetchMock = vi.fn();

  beforeEach(() => {
    originalFetch = globalThis.fetch;
    globalThis.fetch = fetchMock as unknown as typeof globalThis.fetch;
    fetchMock.mockReset();
  });

  afterEach(() => {
    globalThis.fetch = originalFetch;
    vi.useRealTimers();
  });

  it("retries exactly once after a 429 with Retry-After, then succeeds", async () => {
    vi.useFakeTimers();
    fetchMock
      .mockResolvedValueOnce(
        new Response("", { status: 429, headers: { "retry-after": "0" } }),
      )
      .mockResolvedValueOnce(new Response(JSON.stringify({ ok: true })));

    const client = new ApiClient();
    const promise = client.get<{ ok: boolean }>("/api/x");
    // The retry-after is 0s, so advance the fake clock past
    // zero and let the queued retry resolve.
    await vi.advanceTimersByTimeAsync(0);
    const result = await promise;

    expect(result.ok).toBe(true);
    expect(fetchMock).toHaveBeenCalledTimes(2);
  });

  it("after the second 429 throws RateLimited AppError", async () => {
    vi.useFakeTimers();
    fetchMock.mockResolvedValue(
      new Response("", { status: 429, headers: { "retry-after": "0" } }),
    );

    const client = new ApiClient();
    const promise = client.get("/api/x").catch((e) => e);
    await vi.advanceTimersByTimeAsync(0);
    const err = await promise;

    expect(err).toMatchObject({ category: "RateLimited", status: 429 });
    expect(fetchMock).toHaveBeenCalledTimes(2);
  });

  it("clamps an absurd Retry-After to the 65s ceiling", async () => {
    vi.useFakeTimers();
    fetchMock
      .mockResolvedValueOnce(
        new Response("", { status: 429, headers: { "retry-after": "86400" } }),
      )
      .mockResolvedValueOnce(new Response(JSON.stringify({ ok: true })));

    const client = new ApiClient();
    const promise = client.get<{ ok: boolean }>("/api/x");
    // We should wait at most 65s — advance just past that and
    // expect the retry to fire.
    await vi.advanceTimersByTimeAsync(65_000);
    await promise;
    expect(fetchMock).toHaveBeenCalledTimes(2);
  });

  it("missing or malformed Retry-After skips the retry and surfaces RateLimited immediately", async () => {
    fetchMock.mockResolvedValue(new Response("", { status: 429 }));
    const client = new ApiClient();
    const err = await client.get("/api/x").catch((e) => e);
    expect(err).toMatchObject({ category: "RateLimited", status: 429 });
    // Single attempt only — no retry without a header.
    expect(fetchMock).toHaveBeenCalledTimes(1);
  });
});
