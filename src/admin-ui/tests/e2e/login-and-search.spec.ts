import { expect, test } from "@playwright/test";

/**
 * S12 e2e — golden-path operator flow.
 *
 *  1. Cold-load → login form.
 *  2. Mock login (operator / consigliere — MockAuthClient seeded).
 *  3. Land on Dashboard.
 *  4. Header search → 64-char hex → /transactions/:txid (S5 page).
 */
const TXID = "aabbccdd11223344556677889900aabbccdd11223344556677889900aabbccdd";

test("login → dashboard → tx detail via header search", async ({ page }) => {
  await page.goto("/");
  // Bounce to /login when unauthenticated.
  await expect(page).toHaveURL(/\/login$/);

  await page.getByLabel(/username/i).fill("operator");
  await page.getByLabel(/password/i).fill("consigliere");
  await page.getByRole("button", { name: /sign in/i }).click();

  await expect(page).toHaveURL(/\/dashboard$/);
  await expect(page.getByText(/system healthy|system degraded|system offline/i)).toBeVisible();

  // Drive the header search.
  const search = page.getByPlaceholder(/search/i).first();
  await search.fill(TXID);
  await search.press("Enter");
  await expect(page).toHaveURL(new RegExp(`/transactions/${TXID}`));
  await expect(page.getByTestId("entity-timeline")).toBeVisible();
});
