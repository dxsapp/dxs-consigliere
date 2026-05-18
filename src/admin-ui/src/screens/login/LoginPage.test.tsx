import { render, screen } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { describe, expect, it } from "vitest";
import { LoginPage } from "./LoginPage";
import { ApiClient } from "@/lib/api/client";
import { AuthStore } from "@/stores/root";
import { LANDING_PATH, LOGIN_PATH } from "@/app/routes";

function renderLogin(opts?: { authed?: boolean; from?: string }) {
  const auth = new AuthStore(new ApiClient());
  if (opts?.authed) auth.signInSynthetic("op");
  const initialEntry = opts?.from
    ? { pathname: LOGIN_PATH, state: { from: opts.from } }
    : LOGIN_PATH;
  return {
    auth,
    ...render(
      <MemoryRouter initialEntries={[initialEntry]}>
        <Routes>
          <Route path={LOGIN_PATH} element={<LoginPage auth={auth} />} />
          <Route
            path={LANDING_PATH}
            element={<div data-testid="landing-stub">landing</div>}
          />
          <Route path="/headers" element={<div data-testid="headers-stub">headers</div>} />
        </Routes>
      </MemoryRouter>
    ),
  };
}

describe("LoginPage (S2 form)", () => {
  it("renders the brand header + sign-in CTA", () => {
    renderLogin();
    expect(screen.getByText(/consigliere admin/i)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /sign in/i })).toBeInTheDocument();
    expect(screen.getByLabelText(/operator name/i)).toBeInTheDocument();
  });

  it("notes that real auth wires in S3", () => {
    renderLogin();
    expect(screen.getByText(/S2 placeholder/i)).toBeInTheDocument();
  });

  it("authed visitor on /login bounces to landing (S2-audit L1)", () => {
    renderLogin({ authed: true });
    expect(screen.getByTestId("landing-stub")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /sign in/i })).toBeNull();
  });

  it("authed visitor on /login with state.from bounces to the captured path (S2-audit L1)", () => {
    renderLogin({ authed: true, from: "/headers?height=42" });
    expect(screen.getByTestId("headers-stub")).toBeInTheDocument();
  });
});
