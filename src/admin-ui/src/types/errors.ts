/**
 * Normalized application error model. Every HTTP / network /
 * business / auth error in the system maps onto this shape BEFORE
 * reaching a MobX store. Per Core Rule §8.
 *
 * 401 / 403 are first-class: the API client redirects 401 to
 * /login and surfaces 403 as a `Forbidden` error category that
 * the routing layer renders as a forbidden page.
 */
export type AppErrorCategory =
  | "Network" // fetch failed / offline
  | "Timeout" // explicit timeout
  | "Unauthorized" // 401 — session expired or absent
  | "Forbidden" // 403 — authenticated but lacks permission
  | "NotFound" // 404 from a known endpoint
  | "RateLimited" // 429 — wave-A3 S1 rate-limit cutoff after one retry
  | "Validation" // 4xx with backend-provided detail
  | "Server" // 5xx
  | "Unknown"; // anything else

export interface AppError {
  category: AppErrorCategory;
  /** Human-readable message; safe to render. */
  message: string;
  /** HTTP status when applicable. */
  status?: number;
  /** Backend-provided trace id when present. */
  traceId?: string;
}

export function makeAppError(
  category: AppErrorCategory,
  message: string,
  status?: number,
  traceId?: string
): AppError {
  return { category, message, status, traceId };
}
