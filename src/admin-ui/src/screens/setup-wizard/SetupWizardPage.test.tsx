import { act, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { describe, expect, it } from "vitest";
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

describe("SetupWizardPage", () => {
  it("renders all four step labels + lands on step 1", async () => {
    renderWizard();
    await waitFor(() => {
      expect(screen.getByTestId("setup-step-1")).toBeInTheDocument();
    });
    expect(screen.getByText("Admin account")).toBeInTheDocument();
    expect(screen.getByText("Providers")).toBeInTheDocument();
    expect(screen.getByText("Block sync")).toBeInTheDocument();
    expect(screen.getByText("Review")).toBeInTheDocument();
  });

  it("Continue is disabled until step 1 is valid", async () => {
    renderWizard();
    await waitFor(() => {
      expect(screen.getByTestId("setup-step-1")).toBeInTheDocument();
    });
    const cta = screen.getByRole("button", { name: /continue/i });
    expect(cta).toBeDisabled();

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
    await waitFor(() => {
      expect(screen.getByRole("button", { name: /continue/i })).not.toBeDisabled();
    });
  });

  it("walks all the way to step 4 + the Complete setup button appears", async () => {
    renderWizard();
    await waitFor(() => {
      expect(screen.getByTestId("setup-step-1")).toBeInTheDocument();
    });

    act(() => {
      fireEvent.change(screen.getByLabelText(/operator name/i), { target: { value: "admin-a2" } });
      fireEvent.change(screen.getAllByLabelText(/^password/i)[0], { target: { value: "ConsigliereA2!" } });
      fireEvent.change(screen.getByLabelText(/confirm password/i), { target: { value: "ConsigliereA2!" } });
    });
    fireEvent.click(screen.getByRole("button", { name: /continue/i }));

    await waitFor(() => expect(screen.getByTestId("setup-step-2")).toBeInTheDocument());
    fireEvent.click(screen.getByRole("button", { name: /continue/i }));

    await waitFor(() => expect(screen.getByTestId("setup-step-3")).toBeInTheDocument());
    act(() => {
      fireEvent.change(screen.getByLabelText(/block subscription id/i), {
        target: { value: "smoke-sub" },
      });
    });
    fireEvent.click(screen.getByRole("button", { name: /continue/i }));

    await waitFor(() => expect(screen.getByTestId("setup-step-4")).toBeInTheDocument());
    expect(screen.getByRole("button", { name: /complete setup/i })).not.toBeDisabled();
  });

  it("redirects to /login on submit success", async () => {
    renderWizard();
    await waitFor(() => expect(screen.getByTestId("setup-step-1")).toBeInTheDocument());

    act(() => {
      fireEvent.change(screen.getByLabelText(/operator name/i), { target: { value: "admin-a2" } });
      fireEvent.change(screen.getAllByLabelText(/^password/i)[0], { target: { value: "ConsigliereA2!" } });
      fireEvent.change(screen.getByLabelText(/confirm password/i), { target: { value: "ConsigliereA2!" } });
    });
    fireEvent.click(screen.getByRole("button", { name: /continue/i }));
    await waitFor(() => expect(screen.getByTestId("setup-step-2")).toBeInTheDocument());
    fireEvent.click(screen.getByRole("button", { name: /continue/i }));
    await waitFor(() => expect(screen.getByTestId("setup-step-3")).toBeInTheDocument());
    act(() => {
      fireEvent.change(screen.getByLabelText(/block subscription id/i), {
        target: { value: "smoke-sub" },
      });
    });
    fireEvent.click(screen.getByRole("button", { name: /continue/i }));
    await waitFor(() => expect(screen.getByTestId("setup-step-4")).toBeInTheDocument());

    fireEvent.click(screen.getByRole("button", { name: /complete setup/i }));
    await waitFor(() => {
      expect(screen.getByTestId("login-landing")).toBeInTheDocument();
    });
  });
});
