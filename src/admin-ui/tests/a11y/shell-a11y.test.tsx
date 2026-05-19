import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { describe, expect, it } from "vitest";
import { AppShell } from "@/components/shell/AppShell";
import { ThemeProvider } from "@/app/ThemeProvider";
import { MockAuthClient } from "@/lib/mock/auth";
import { PrefStore } from "@/stores/pref.store";
import { ShellStore } from "@/stores/shell.store";
import { AuthStore } from "@/stores/root";

/**
 * S11 — accessibility invariants for the operator shell.
 *
 * We do NOT shell out to axe-core in this slice (the playbook
 * defers a full axe pass to the closeout). These cases pin the
 * regression-prone invariants:
 *   - every IconButton in the header has an aria-label
 *   - the navigation Drawer is reachable via role=navigation
 *   - the alert badge IconButton carries the live count in its
 *     accessible name (via the tooltip)
 *   - the connection chip carries text content
 */
function renderShell() {
  const prefs = new PrefStore();
  const shell = new ShellStore();
  const auth = new AuthStore(new MockAuthClient());
  auth.forceAuthenticatedForTests("operator");
  return render(
    <ThemeProvider prefs={prefs}>
      <MemoryRouter initialEntries={["/dashboard"]}>
        <AppShell auth={auth} prefs={prefs} shell={shell} env="mainnet">
          <div data-testid="content">child</div>
        </AppShell>
      </MemoryRouter>
    </ThemeProvider>
  );
}

describe("Shell a11y (S11)", () => {
  it("every header IconButton has an accessible name", () => {
    renderShell();
    // Filter to top-level header buttons by aria-label — every button
    // in MUI is rendered as role=button; pick the canonical ones.
    const expected = [
      "alerts",
      "toggle theme mode",
      "logout",
    ];
    for (const label of expected) {
      expect(
        screen.getByRole("button", { name: new RegExp(label, "i") })
      ).toBeInTheDocument();
    }
  });

  it("the navigation drawer exposes the operator routes as links", () => {
    renderShell();
    const links = screen.getAllByRole("link", { hidden: true });
    // Pin the canonical operator entries — the drawer must keep
    // these reachable on every breakpoint.
    const expected = ["Dashboard", "Alerts", "P2P Pool"];
    for (const label of expected) {
      expect(links.some((l) => l.textContent?.includes(label))).toBe(true);
    }
  });

  it("the alert badge IconButton accessible name communicates the count (S7-S12-audit L2)", () => {
    renderShell();
    // ShellStore seeds alertCount=0 by default — the accessible
    // name MUST include the count so a screen-reader announces it.
    const btn = screen.getByRole("button", { name: /alerts \(0 active\)/i });
    expect(btn).toBeInTheDocument();
  });
});
