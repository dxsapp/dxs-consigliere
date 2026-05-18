/**
 * Admin auth DTOs — TS mirror of the C# shapes at
 * `src/Dxs.Consigliere/Dto/{Requests,Responses}/AdminAuth*.cs`.
 *
 * S3 ships the hand-written types here; the S3 followup wires a
 * codegen step against `src/admin-ui/contracts/swagger.json` so this
 * file becomes auto-generated.
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
