import { act, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { describe, expect, it, vi } from "vitest";
import { SetupWizardPage } from "./SetupWizardPage";
import { ThemeProvider } from "@/app/ThemeProvider";
import { MockAdminClient } from "@/lib/mock/admin";
import { MockAuthClient } from "@/lib/mock/auth";
import { PrefStore } from "@/stores/pref.store";
import { AuthStore } from "@/stores/root";

function renderWizard() {
  const prefs = new PrefStore();
  const admin = new MockAdminClient();
  const auth = new AuthStore(new MockAuthClient());
  const result = render(
    <ThemeProvider prefs={prefs}>
      <MemoryRouter initialEntries={["/setup"]}>
        <Routes>
          <Route path="/setup" element={<SetupWizardPage admin={admin} auth={auth} />} />
          <Route path="/login" element={<div data-testid="login-landing">login</div>} />
        </Routes>
      </MemoryRouter>
    </ThemeProvider>
  );
  return { admin, auth, ...result };
}

function fillAdmin() {
  act(() => {
    fireEvent.change(screen.getByLabelText(/operator name/i), {
      target: { value: "admin-a2" },
    });
    // MUI duplicates the password label across confirm; pin by name
    fireEvent.change(screen.getAllByLabelText(/^password/i)[0], {
      target: { value: "ConsigliereA2!" },
    });
    fireEvent.change(screen.getByLabelText(/confirm password/i), {
      target: { value: "ConsigliereA2!" },
    });
  });
}

describe("SetupWizardPage", () => {
  it("renders the single admin-account step (no provider/block-sync steps)", async () => {
    renderWizard();
    await waitFor(() => {
      expect(screen.getByTestId("setup-step-1")).toBeInTheDocument();
    });
    expect(screen.getByText(/create your admin account/i)).toBeInTheDocument();
    // The old multi-step rail is gone.
    expect(screen.queryByText("Providers")).not.toBeInTheDocument();
    expect(screen.queryByText("Block sync")).not.toBeInTheDocument();
    expect(screen.queryByText("Review")).not.toBeInTheDocument();
  });

  it("Create account is disabled until the admin form is valid", async () => {
    renderWizard();
    await waitFor(() => {
      expect(screen.getByTestId("setup-step-1")).toBeInTheDocument();
    });
    const cta = screen.getByRole("button", { name: /create account/i });
    expect(cta).toBeDisabled();

    fillAdmin();
    await waitFor(() => {
      expect(screen.getByRole("button", { name: /create account/i })).not.toBeDisabled();
    });
  });

  it("redirects to /login on submit success", async () => {
    renderWizard();
    await waitFor(() => expect(screen.getByTestId("setup-step-1")).toBeInTheDocument());

    fillAdmin();
    fireEvent.click(screen.getByRole("button", { name: /create account/i }));
    await waitFor(() => {
      expect(screen.getByTestId("login-landing")).toBeInTheDocument();
    });
  });

  it("clears auth.setupRequired post-submit even if hydrate fails (S0-audit M1)", async () => {
    const prefs = new PrefStore();
    const admin = new MockAdminClient();
    const authClient = new MockAuthClient();
    // Seed: pretend the backend still reports setupRequired=true
    // (mock localStorage is empty so MockAuthClient.me() does
    // exactly that on the initial hydrate).
    const auth = new AuthStore(authClient);
    await auth.hydrate();
    expect(auth.setupRequired).toBe(true);

    // Simulate a transient /me failure on the post-submit hydrate.
    vi.spyOn(authClient, "me").mockRejectedValueOnce(new Error("network blip"));

    render(
      <ThemeProvider prefs={prefs}>
        <MemoryRouter initialEntries={["/setup"]}>
          <Routes>
            <Route path="/setup" element={<SetupWizardPage admin={admin} auth={auth} />} />
            <Route path="/login" element={<div data-testid="login-landing">login</div>} />
          </Routes>
        </MemoryRouter>
      </ThemeProvider>
    );

    await waitFor(() => expect(screen.getByTestId("setup-step-1")).toBeInTheDocument());
    fillAdmin();
    fireEvent.click(screen.getByRole("button", { name: /create account/i }));

    // Even with the broken /me, the wizard must redirect AND
    // auth.setupRequired must be false (so the LoginPage banner
    // doesn't bounce the operator back to /setup).
    await waitFor(() => {
      expect(screen.getByTestId("login-landing")).toBeInTheDocument();
    });
    expect(auth.setupRequired).toBe(false);
  });

  it("renders a neutral loader before options resolve (S0-audit L1)", async () => {
    const prefs = new PrefStore();
    const admin = new MockAdminClient();
    const auth = new AuthStore(new MockAuthClient());
    // Hold getSetupOptions open so the page is stuck in `loading`.
    vi.spyOn(admin, "getSetupOptions").mockImplementation(
      () => new Promise(() => {
        /* never resolves */
      })
    );
    render(
      <ThemeProvider prefs={prefs}>
        <MemoryRouter initialEntries={["/setup"]}>
          <Routes>
            <Route path="/setup" element={<SetupWizardPage admin={admin} auth={auth} />} />
          </Routes>
        </MemoryRouter>
      </ThemeProvider>
    );
    expect(screen.getByTestId("setup-wizard-loading")).toBeInTheDocument();
    // No wizard chrome flashes while options are in flight.
    expect(screen.queryByText(/create your admin account/i)).not.toBeInTheDocument();
    expect(screen.queryByTestId("setup-step-1")).not.toBeInTheDocument();
  });
});
