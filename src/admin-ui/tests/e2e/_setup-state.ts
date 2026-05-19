import type { Page } from "@playwright/test";

/**
 * wave-A2 S0 — every authed e2e spec must pre-seed the mock
 * setup-completed flag, otherwise AuthGuard bounces an
 * unauthenticated visitor to `/setup` (the new first-run wizard)
 * rather than `/login`. Call this once at the top of each spec
 * that exercises a post-setup flow.
 *
 * The setup-wizard spec deliberately does NOT call this — it
 * starts from a fresh install.
 */
export async function seedSetupCompleted(page: Page): Promise<void> {
  await page.addInitScript(() => {
    try {
      window.localStorage.setItem(
        "consigliere-admin/mock-setup-state/v1",
        JSON.stringify({
          setupRequired: false,
          setupCompleted: true,
          adminEnabled: true,
          adminUsername: "operator",
        })
      );
    } catch {
      /* swallow */
    }
  });
}
