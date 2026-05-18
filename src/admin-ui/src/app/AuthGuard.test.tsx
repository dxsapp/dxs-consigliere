import { render, screen } from "@testing-library/react";
import { MemoryRouter, Route, Routes, useLocation } from "react-router-dom";
import { describe, expect, it } from "vitest";
import { AuthGuard } from "./AuthGuard";
import { ApiClient } from "@/lib/api/client";
import { AuthStore } from "@/stores/root";
import { LOGIN_PATH } from "@/app/routes";

/**
 * S2-audit M3 + L1: prove the redirect preserves the full URL
 * (path + search + hash) and that an authed visit to /login
 * bounces back to the captured location.
 */

function LoginProbe() {
  const location = useLocation();
  const from = (location.state as { from?: string } | null)?.from ?? "";
  return <div data-testid="login-stub" data-from={from} />;
}

function renderAt(path: string, authed: boolean) {
  const auth = new AuthStore(new ApiClient());
  if (authed) auth.signInSynthetic("op");
  return render(
    <MemoryRouter initialEntries={[path]}>
      <Routes>
        <Route
          path="/*"
          element={
            <AuthGuard auth={auth}>
              <div data-testid="authed">ok</div>
            </AuthGuard>
          }
        />
        <Route path={LOGIN_PATH} element={<LoginProbe />} />
      </Routes>
    </MemoryRouter>
  );
}

describe("AuthGuard URL preservation (S2-audit M3)", () => {
  it("captures pathname + search in state.from", () => {
    renderAt("/headers?height=42", false);
    const stub = screen.getByTestId("login-stub");
    expect(stub.getAttribute("data-from")).toBe("/headers?height=42");
  });

  it("captures pathname + hash in state.from", () => {
    renderAt("/dashboard#row-7", false);
    const stub = screen.getByTestId("login-stub");
    expect(stub.getAttribute("data-from")).toBe("/dashboard#row-7");
  });

  it("captures pathname + search + hash together", () => {
    renderAt("/transactions/abcd?tab=outputs#log-42", false);
    const stub = screen.getByTestId("login-stub");
    expect(stub.getAttribute("data-from")).toBe("/transactions/abcd?tab=outputs#log-42");
  });

  it("authenticated visitor reaches the guarded route directly", () => {
    renderAt("/dashboard", true);
    expect(screen.getByTestId("authed")).toBeInTheDocument();
    expect(screen.queryByTestId("login-stub")).toBeNull();
  });
});
