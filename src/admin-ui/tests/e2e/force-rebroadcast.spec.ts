import { expect, test } from "@playwright/test";

const VALID_HEX = "0100000001" + "a".repeat(64);

/**
 * S12 e2e — destructive-action gating.
 *
 *  - Log in.
 *  - Navigate to Broadcast Queue.
 *  - Open Force-rebroadcast dialog from the page CTA.
 *  - Verify the two-step confirm (Rebroadcast… → Confirm broadcast).
 *  - Mock-mode submit resolves; receipt renders.
 */
test("force-rebroadcast two-step confirm", async ({ page }) => {
  await page.goto("/login");
  await page.getByLabel(/operator name/i).fill("operator");
  await page.getByLabel(/password/i).fill("consigliere");
  await page.getByRole("button", { name: /sign in/i }).click();
  await expect(page).toHaveURL(/\/dashboard$/);

  await page.goto("/broadcast-queue");
  // Wait for the lazy chunk + Suspense fallback to resolve before
  // reaching for the CTA — Suspense fallback is null, so without
  // the wait we'd race the chunk load.
  await expect(page.getByRole("heading", { name: /broadcast queue/i })).toBeVisible();
  await page.getByRole("button", { name: /^force rebroadcast$/i }).click();

  await expect(page.getByRole("dialog")).toBeVisible();
  const input = page.getByLabel(/rawHex/i);
  await input.fill(VALID_HEX);

  // First click flips the CTA to "Confirm broadcast".
  await page.getByRole("button", { name: /rebroadcast…/i }).click();
  await page.getByRole("button", { name: /confirm broadcast/i }).click();

  await expect(page.getByText(/Backend accepted the request/i)).toBeVisible();
});
