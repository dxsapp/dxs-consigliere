import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { describe, expect, it } from "vitest";
import { LoginPage } from "./LoginPage";
import { MockAuthClient } from "@/lib/mock/auth";
import { AuthStore } from "@/stores/root";
import { LANDING_PATH, LOGIN_PATH } from "@/app/routes";

function renderLogin(opts?: { authed?: boolean; from?: string }) {
  const auth = new AuthStore(new MockAuthClient());
  if (opts?.authed) auth.forceAuthenticatedForTests("op");
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

describe("LoginPage (S3 cookie-mode form)", () => {
  it("renders the brand header + sign-in CTA + username + password inputs", () => {
    renderLogin();
    expect(screen.getByText(/consigliere admin/i)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /sign in/i })).toBeInTheDocument();
    expect(screen.getByLabelText(/operator name/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/password/i)).toBeInTheDocument();
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

  it("valid credentials sign in via the auth client and bounce to landing (S3)", async () => {
    renderLogin();
    fireEvent.change(screen.getByLabelText(/operator name/i), {
      target: { value: "operator" },
    });
    fireEvent.change(screen.getByLabelText(/password/i), {
      target: { value: "consigliere" },
    });
    fireEvent.click(screen.getByRole("button", { name: /sign in/i }));
    await waitFor(() => {
      expect(screen.getByTestId("landing-stub")).toBeInTheDocument();
    });
  });

  it("invalid credentials render the backend error code (S3-audit M2)", async () => {
    renderLogin();
    fireEvent.change(screen.getByLabelText(/operator name/i), {
      target: { value: "operator" },
    });
    fireEvent.change(screen.getByLabelText(/password/i), {
      target: { value: "wrong" },
    });
    fireEvent.click(screen.getByRole("button", { name: /sign in/i }));
    // The mock auth client rejects with `invalid_credentials`; the
    // AuthStore surfaces it via lastError and LoginPage renders it.
    await waitFor(() => {
      expect(screen.getByText(/invalid_credentials/)).toBeInTheDocument();
    });
    // Form stays — no landing bounce; inputs are re-enabled.
    expect(screen.queryByTestId("landing-stub")).toBeNull();
    expect(screen.getByLabelText(/operator name/i)).toBeEnabled();
  });
});
