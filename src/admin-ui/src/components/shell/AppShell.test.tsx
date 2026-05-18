import { render, screen } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { describe, expect, it } from "vitest";
import { AppShell } from "./AppShell";
import { AuthGuard } from "@/app/AuthGuard";
import { ThemeProvider } from "@/app/ThemeProvider";
import { MockAuthClient } from "@/lib/mock/auth";
import { AuthStore } from "@/stores/root";
import { PrefStore } from "@/stores/pref.store";
import { ShellStore } from "@/stores/shell.store";
import { LOGIN_PATH } from "@/app/routes";

function build(authed: boolean) {
  const prefs = new PrefStore();
  const auth = new AuthStore(new MockAuthClient());
  const shell = new ShellStore();
  if (authed) auth.forceAuthenticatedForTests("op");
  return { prefs, auth, shell };
}

function renderGuarded(initialPath: string, opts?: { authed?: boolean }) {
  const ctx = build(opts?.authed ?? true);
  const utils = render(
    <ThemeProvider prefs={ctx.prefs}>
      <MemoryRouter initialEntries={[initialPath]}>
        <Routes>
          <Route
            path="/*"
            element={
              <AuthGuard auth={ctx.auth}>
                <AppShell auth={ctx.auth} prefs={ctx.prefs} shell={ctx.shell} env="dev">
                  <div data-testid="authed-content">authed content</div>
                </AppShell>
              </AuthGuard>
            }
          />
          <Route path={LOGIN_PATH} element={<div data-testid="login-stub">login</div>} />
        </Routes>
      </MemoryRouter>
    </ThemeProvider>
  );
  return { ...ctx, ...utils };
}

describe("AppShell + AuthGuard wiring", () => {
  it("unauthenticated visitor is redirected to /login", () => {
    renderGuarded("/dashboard", { authed: false });
    expect(screen.getByTestId("login-stub")).toBeInTheDocument();
    expect(screen.queryByTestId("authed-content")).toBeNull();
  });

  it("authenticated visitor sees the shell + content + Operator nav", () => {
    renderGuarded("/dashboard", { authed: true });
    expect(screen.getByTestId("authed-content")).toBeInTheDocument();
    expect(screen.getByText(/^Dashboard$/)).toBeInTheDocument();
    expect(screen.getByText(/^P2P Pool$/)).toBeInTheDocument();
    expect(screen.getByText("DEV")).toBeInTheDocument();
    expect(screen.getByText("dev")).toBeInTheDocument();
  });

  it("Transactions nav stays active on the :txid detail route (S2-audit M1)", () => {
    renderGuarded("/transactions/aabbccdd", { authed: true });
    const transactionsLink = screen
      .getAllByRole("link", { hidden: true })
      .find((a) => a.textContent?.includes("Transactions"));
    expect(transactionsLink).toBeDefined();
    expect(transactionsLink?.className).toMatch(/active/);
  });

  it("Dashboard nav is NOT active when on /transactions (no false prefix-match)", () => {
    renderGuarded("/transactions", { authed: true });
    const dashboardLink = screen
      .getAllByRole("link", { hidden: true })
      .find((a) => a.textContent?.includes("Dashboard"));
    expect(dashboardLink).toBeDefined();
    expect(dashboardLink?.className ?? "").not.toMatch(/active/);
  });
});
