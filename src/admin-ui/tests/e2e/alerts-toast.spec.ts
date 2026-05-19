import { expect, test } from "@playwright/test";

/**
 * S7-S12-audit M1 — closeout golden path that the static
 * `alerts.spec.ts` does NOT cover: the toast lifecycle.
 *
 * Mock mode (`VITE_API_MODE=mock`, the dev profile booted by
 * playwright.config.ts) routes alerts through `MockAdminClient`,
 * which bypasses HTTP — so a Playwright `page.route()` intercept
 * cannot inject a fake poll. Instead this spec exercises the
 * same code path: when the page mounts, `AlertsStore` reports
 * fresh alerts in `newSinceLastTick`, the page's drain effect
 * pushes them onto the toast queue, and each Snackbar appears
 * with the `alert-toast-<id>` test-id.
 *
 * Asserting against the mock seed (`alert-001`) keeps the spec
 * deterministic + free of timing fragility.
 */
test("alerts page surfaces a Snackbar toast for the freshest alert", async ({ page }) => {
  await page.goto("/login");
  await page.getByLabel(/operator name/i).fill("operator");
  await page.getByLabel(/password/i).fill("consigliere");
  await page.getByRole("button", { name: /sign in/i }).click();
  await expect(page).toHaveURL(/\/dashboard$/);

  await page.goto("/alerts");
  // Wait for the page chunk to land before pinning the toast.
  await expect(page.getByText("S7")).toBeVisible({ timeout: 15_000 });

  // The mock seeds `alert-001` ~90s old (active) and three older
  // entries (history). On first poll, AlertsStore reports all four
  // as `newSinceLastTick`; the page's drain effect spawns toasts.
  // We pin the recent one — the other three exit history quickly.
  await expect(page.getByTestId("alert-toast-alert-001")).toBeVisible({
    timeout: 15_000,
  });
});
