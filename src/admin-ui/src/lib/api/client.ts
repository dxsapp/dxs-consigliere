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
    let response: Response;
    try {
      response = await fetch(url, {
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

// Re-export the error shape for convenience.
export type { AppError };
