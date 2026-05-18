import { expect, test } from "@playwright/test";

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
  await page.goto("/login");
  await page.getByLabel(/username/i).fill("operator");
  await page.getByLabel(/password/i).fill("consigliere");
  await page.getByRole("button", { name: /sign in/i }).click();
  await expect(page).toHaveURL(/\/dashboard$/);

  await page.goto("/alerts");
  await expect(page.getByRole("heading", { name: /alerts/i })).toBeVisible();
  await expect(page.getByText("Active")).toBeVisible();
  await expect(page.getByText("History")).toBeVisible();

  // Mock seed includes alert-001 ~90s old → active.
  await expect(page.getByTestId("alert-active-alert-001")).toBeVisible({ timeout: 10_000 });
});
