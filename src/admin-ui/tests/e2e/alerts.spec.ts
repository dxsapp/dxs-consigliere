import { expect, test } from "@playwright/test";
import { seedSetupCompleted } from "./_setup-state";

/**
 * S12 e2e — alerts surface.
 *
 *  - Log in.
 *  - Navigate to /alerts.
 *  - Verify both active + history sections render.
 *  - At least one mock alert renders as an active card (mock seeds
 *    `alert-001` within the activeWindow).
 */
test("alerts page renders active cards", async ({ page }) => {
  await seedSetupCompleted(page);
  await page.goto("/login");
  await page.getByLabel(/operator name/i).fill("operator");
  await page.getByLabel(/password/i).fill("consigliere");
  await page.getByRole("button", { name: /sign in/i }).click();
  await expect(page).toHaveURL(/\/dashboard$/);

  await page.goto("/alerts");
  // The page chunk is lazy + behind Suspense fallback=null; wait
  // for the active card from the mock seed.
  await expect(page.getByTestId("alert-active-alert-001")).toBeVisible({
    timeout: 15_000,
  });
  // Now that the page is fully painted, verify the two card sections.
  await expect(page.getByText("Active", { exact: true })).toBeVisible();
  await expect(page.getByText("History", { exact: true })).toBeVisible();
});
