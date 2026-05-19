/**
 * Admin auth DTOs — TS mirror of the C# shapes at
 * `src/Dxs.Consigliere/Dto/{Requests,Responses}/AdminAuth*.cs`.
 *
 * wave-A2 S1 shipped the codegen gate
 * (`src/types/api.generated.ts` + `pnpm contracts:check`) which
 * fails CI red on any backend DTO drift. The hand-mirrored types
 * in this file stay as the screen-side source for now — see the
 * companion `src/types/admin.ts` header for the rationale + the
 * wave-A3 NRT migration plan.
 */

export interface AdminAuthStatusResponse {
  /** First-run / no-credentials-configured signal. */
  setupRequired: boolean;
  /** Backend-side auth feature flag. */
  enabled: boolean;
  /** Caller has a valid session. */
  authenticated: boolean;
  /** Auth scheme — currently always "cookie". */
  mode: string;
  /** Empty string when anonymous. */
  username: string;
  /** Session lifetime in minutes; null when disabled. */
  sessionTtlMinutes: number | null;
}

export interface AdminLoginRequest {
  username: string;
  password: string;
}

/**
 * Backend-defined error codes for the login endpoint. Surfaced to
 * the UI via AppError.message so the form can render specific copy.
 */
export type AdminLoginErrorCode =
  | "setup_required" // 409
  | "credentials_required" // 400
  | "invalid_credentials" // 401
  | "unknown";
