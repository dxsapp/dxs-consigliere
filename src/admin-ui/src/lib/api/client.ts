import { type AppError, makeAppError } from "@/types/errors";

/**
 * S0 baseline API client. Full implementation (auth client, SignalR
 * client, mock-mode switch, contract-parity test) lands in S3.
 *
 * This file ships the 401-redirect contract so the auth scaffold is
 * functional from S0: any 401 from a backend call sends the user to
 * /login and rejects the promise with an `Unauthorized` AppError.
 */
export interface FetchOptions {
  signal?: AbortSignal;
}

/**
 * wave-A3 S1: hard ceiling on the single Retry-After honour
 * window. The backend's worst-case Retry-After under the
 * conservative defaults is 60s (the login policy resets every
 * minute), so 65s gives us a small safety margin. A buggy /
 * malicious server sending `Retry-After: 86400` cannot make
 * the UI hang for a day.
 */
const MAX_RETRY_AFTER_MS = 65_000;

export class ApiClient {
  /** Base path; in dev the Vite proxy forwards /api → ASP.NET host. */
  private readonly base: string;

  constructor(base = "") {
    this.base = base;
  }

  async get<T>(path: string, opts: FetchOptions = {}): Promise<T> {
    return this.request<T>("GET", path, undefined, opts);
  }

  async post<T>(path: string, body: unknown, opts: FetchOptions = {}): Promise<T> {
    return this.request<T>("POST", path, body, opts);
  }

  private async request<T>(
    method: string,
    path: string,
    body: unknown,
    opts: FetchOptions
  ): Promise<T> {
    const url = `${this.base}${path}`;
    const send = async (): Promise<Response> => {
      try {
        return await fetch(url, {
          method,
          credentials: "include", // cookie auth
          headers: body !== undefined ? { "Content-Type": "application/json" } : undefined,
          body: body !== undefined ? JSON.stringify(body) : undefined,
          signal: opts.signal,
        });
      } catch (err) {
        if (opts.signal?.aborted) {
          throw makeAppError("Timeout", "Request aborted");
        }
        throw makeAppError("Network", (err as Error).message ?? "Network error");
      }
    };

    let response = await send();

    // wave-A3 S1: honour `Retry-After` once. The backend (a)
    // returns 429 only from the rate-limit middleware, (b)
    // always emits the header. We sleep the indicated duration
    // and retry exactly one more time. Subsequent 429 surfaces
    // as a `RateLimited` AppError so the UI can render a
    // calm-down banner instead of looping.
    if (response.status === 429) {
      const wait = parseRetryAfterMs(response.headers.get("retry-after"));
      if (wait !== null) {
        await sleep(wait, opts.signal);
        response = await send();
      }
      if (response.status === 429) {
        throw makeAppError(
          "RateLimited",
          "Too many requests. Slow down and try again in a moment.",
          429,
        );
      }
    }

    if (response.status === 401) {
      // Core Rule §13 auth gate: an unauthenticated response bounces
      // the user back to the login screen. The store layer still gets
      // the AppError so it can suppress further work.
      if (typeof window !== "undefined" && window.location.pathname !== "/login") {
        window.location.assign("/login");
      }
      throw makeAppError("Unauthorized", "Session expired or absent", 401);
    }
    if (response.status === 403) {
      throw makeAppError("Forbidden", "You do not have permission for this resource", 403);
    }
    if (response.status === 404) {
      throw makeAppError("NotFound", `Not found: ${path}`, 404);
    }
    if (response.status >= 500) {
      throw makeAppError("Server", `Server error ${response.status}`, response.status);
    }
    if (!response.ok) {
      const detail = await safeReadText(response);
      throw makeAppError(
        "Validation",
        detail || `Request failed with ${response.status}`,
        response.status
      );
    }

    // 204 No Content
    if (response.status === 204) return undefined as T;

    return (await response.json()) as T;
  }
}

async function safeReadText(response: Response): Promise<string | undefined> {
  try {
    return await response.text();
  } catch {
    return undefined;
  }
}

/**
 * Parses the standard HTTP `Retry-After` header. RFC 7231
 * allows two forms — delta-seconds and HTTP-date — but our
 * backend only emits delta-seconds. Returns null on malformed
 * input so the caller drops back to "no retry."
 */
function parseRetryAfterMs(header: string | null): number | null {
  if (!header) return null;
  const seconds = Number(header);
  if (!Number.isFinite(seconds) || seconds < 0) return null;
  return Math.min(seconds * 1000, MAX_RETRY_AFTER_MS);
}

function sleep(ms: number, signal?: AbortSignal): Promise<void> {
  return new Promise((resolveSleep, rejectSleep) => {
    const timer = setTimeout(() => {
      signal?.removeEventListener("abort", onAbort);
      resolveSleep();
    }, ms);
    const onAbort = () => {
      clearTimeout(timer);
      rejectSleep(makeAppError("Timeout", "Request aborted"));
    };
    signal?.addEventListener("abort", onAbort, { once: true });
  });
}

// Re-export the error shape for convenience.
export type { AppError };
