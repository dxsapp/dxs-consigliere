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

  await page.getByLabel(/operator name/i).fill("operator");
  await page.getByLabel(/password/i).fill("consigliere");
  await page.getByRole("button", { name: /sign in/i }).click();

  await expect(page).toHaveURL(/\/dashboard$/);
  // Dashboard chunk is lazy; wait until the hero is on-screen.
  await expect(page.getByText(/system healthy|system degraded|system offline/i)).toBeVisible();

  // Drive the header search. The shell hosts one search field; the
  // Dashboard's QuickLookup card hosts another with the same
  // placeholder. The header sits in the page banner — pick it via
  // the AppBar landmark to avoid ambiguity.
  const headerSearch = page
    .getByRole("banner")
    .getByPlaceholder(/search/i);
  await headerSearch.focus();
  await page.keyboard.type(TXID);
  await expect(headerSearch).toHaveValue(TXID);
  // A 64-hex input is `ambiguous` per parseSearchQuery (could be tx
  // OR block-hash). The grammar opens the "did you mean" dropdown
  // on Enter and waits for the operator to pick. Click the tx
  // candidate to navigate to /transactions/:txid.
  await page.keyboard.press("Enter");
  await page.getByRole("option", { name: /transaction/i }).click();
  await expect(page).toHaveURL(new RegExp(`/transactions/${TXID}`));
  await expect(page.getByTestId("entity-timeline")).toBeVisible();
});
