/**
 * Admin auth DTOs. wave-A4 S3 — the hand-mirrored interfaces are
 * now generated re-exports of `components["schemas"][...]`; the
 * backing C# DTOs (`AdminAuthStatusResponse`, `AdminLoginRequest`)
 * were migrated to `#nullable enable` so the generated shapes are
 * tight. The `AdminLoginErrorCode` union below is NOT a generated
 * object shape (a string enum) and stays a literal helper.
 */
import type { components } from "@/types/api.generated";

export type AdminAuthStatusResponse = components["schemas"]["AdminAuthStatusResponse"];

export type AdminLoginRequest = components["schemas"]["AdminLoginRequest"];

/**
 * Backend-defined error codes for the login endpoint. Surfaced to
 * the UI via AppError.message so the form can render specific copy.
 */
export type AdminLoginErrorCode =
  | "setup_required" // 409
  | "credentials_required" // 400
  | "invalid_credentials" // 401
  | "unknown";
