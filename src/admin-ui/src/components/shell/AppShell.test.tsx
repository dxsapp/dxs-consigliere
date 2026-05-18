import { render, screen } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { describe, expect, it } from "vitest";
import { AppShell } from "./AppShell";
import { AuthGuard } from "@/app/AuthGuard";
import { ThemeProvider } from "@/app/ThemeProvider";
import { ApiClient } from "@/lib/api/client";
import { AuthStore } from "@/stores/root";
import { PrefStore } from "@/stores/pref.store";
import { LOGIN_PATH } from "@/app/routes";

function build(authed: boolean) {
  const prefs = new PrefStore();
  const auth = new AuthStore(new ApiClient());
  if (authed) auth.signInSynthetic("op");
  return { prefs, auth };
}

describe("AppShell + AuthGuard wiring", () => {
  it("unauthenticated visitor is redirected to /login", () => {
    const { prefs, auth } = build(false);
    render(
      <ThemeProvider prefs={prefs}>
        <MemoryRouter initialEntries={["/dashboard"]}>
          <Routes>
            <Route
              path="/*"
              element={
                <AuthGuard auth={auth}>
                  <AppShell auth={auth} prefs={prefs} env="dev">
                    <div data-testid="authed-content">dashboard</div>
                  </AppShell>
                </AuthGuard>
              }
            />
            <Route path={LOGIN_PATH} element={<div data-testid="login-stub">login</div>} />
          </Routes>
        </MemoryRouter>
      </ThemeProvider>
    );
    expect(screen.getByTestId("login-stub")).toBeInTheDocument();
    expect(screen.queryByTestId("authed-content")).toBeNull();
  });

  it("authenticated visitor sees the shell + content + Operator nav", () => {
    const { prefs, auth } = build(true);
    render(
      <ThemeProvider prefs={prefs}>
        <MemoryRouter initialEntries={["/dashboard"]}>
          <Routes>
            <Route
              path="/*"
              element={
                <AuthGuard auth={auth}>
                  <AppShell auth={auth} prefs={prefs} env="dev">
                    <div data-testid="authed-content">dashboard</div>
                  </AppShell>
                </AuthGuard>
              }
            />
          </Routes>
        </MemoryRouter>
      </ThemeProvider>
    );
    expect(screen.getByTestId("authed-content")).toBeInTheDocument();
    // Sidebar entries — both sections render.
    expect(screen.getByText(/^Dashboard$/)).toBeInTheDocument();
    expect(screen.getByText(/^P2P Pool$/)).toBeInTheDocument();
    // The "DEV" chip on the System section.
    expect(screen.getByText("DEV")).toBeInTheDocument();
    // Env tag in the header.
    expect(screen.getByText("dev")).toBeInTheDocument();
  });
});
