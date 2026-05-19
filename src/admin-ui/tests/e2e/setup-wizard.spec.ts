import { expect, test } from "@playwright/test";

/**
 * wave-A2 S0 — end-to-end gate for the public first-run setup
 * wizard. Each spec runs in a fresh browser context so localStorage
 * starts empty; the mock backend reports setupRequired:true.
 *
 * The spec proves the chicken-and-egg fix from wave-A1:
 *   1. Anonymous visit to `/` → bounced to `/setup` (NOT /login).
 *   2. Walk all 4 wizard steps.
 *   3. Submit → mock flips setupCompleted=true + stashes the
 *      chosen credentials.
 *   4. Redirected to `/login` → sign in with those credentials
 *      → land in the admin shell.
 */

const ADMIN_USERNAME = "admin-a2";
const ADMIN_PASSWORD = "ConsigliereA2!";
const BLOCK_SUB_ID = "smoke-sub-001";

test("first-run wizard completes + sign-in works with chosen credentials", async ({ page }) => {
  // Step 0 — anonymous visit lands on the wizard (not /login).
  await page.goto("/");
  await expect(page).toHaveURL(/\/setup$/, { timeout: 15_000 });
  await expect(page.getByText(/first-run setup/i)).toBeVisible();

  // Step 1 — admin account. Two TextFields share the /password/i
  // label; pin by exact accessible name.
  await expect(page.getByTestId("setup-step-1")).toBeVisible({ timeout: 15_000 });
  await page.getByLabel(/operator name/i).fill(ADMIN_USERNAME);
  await page.getByLabel("Password", { exact: true }).fill(ADMIN_PASSWORD);
  await page.getByLabel("Confirm password").fill(ADMIN_PASSWORD);
  await page.getByRole("button", { name: /continue/i }).click();

  // Step 2 — providers (defaults are pre-filled).
  await expect(page.getByTestId("setup-step-2")).toBeVisible();
  await page.getByRole("button", { name: /continue/i }).click();

  // Step 3 — block sync. Subscription ID is required.
  await expect(page.getByTestId("setup-step-3")).toBeVisible();
  await page.getByLabel(/block subscription id/i).fill(BLOCK_SUB_ID);
  await page.getByRole("button", { name: /continue/i }).click();

  // Step 4 — review + complete.
  await expect(page.getByTestId("setup-step-4")).toBeVisible();
  await expect(page.getByText(ADMIN_USERNAME)).toBeVisible();
  await page.getByRole("button", { name: /complete setup/i }).click();

  // Redirect to /login.
  await expect(page).toHaveURL(/\/login$/, { timeout: 10_000 });
  // Setup-required banner is gone after a completed install.
  await expect(page.getByTestId("login-go-to-setup")).toHaveCount(0);

  // Sign in with the freshly-chosen credentials.
  await page.getByLabel(/operator name/i).fill(ADMIN_USERNAME);
  await page.getByLabel(/password/i).fill(ADMIN_PASSWORD);
  await page.getByRole("button", { name: /sign in/i }).click();

  // Land in the admin shell.
  await expect(page).toHaveURL(/\/dashboard$/, { timeout: 15_000 });
});
