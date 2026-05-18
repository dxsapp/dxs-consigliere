import { render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { describe, expect, it } from "vitest";
import { AlertsPage } from "./AlertsPage";
import { ThemeProvider } from "@/app/ThemeProvider";
import { MockAdminClient } from "@/lib/mock/admin";
import { PrefStore } from "@/stores/pref.store";

function renderPage() {
  const prefs = new PrefStore();
  const admin = new MockAdminClient();
  return render(
    <ThemeProvider prefs={prefs}>
      <MemoryRouter initialEntries={["/alerts"]}>
        <AlertsPage admin={admin} />
      </MemoryRouter>
    </ThemeProvider>
  );
}

describe("AlertsPage", () => {
  it("renders the page chrome + active/history sections", async () => {
    renderPage();
    expect(screen.getByRole("heading", { name: /Alerts/i })).toBeInTheDocument();
    expect(screen.getByText("Active")).toBeInTheDocument();
    expect(screen.getByText("History")).toBeInTheDocument();
    // The active count chip flips off once alerts load.
    await waitFor(() => {
      expect(screen.queryByText(/^0 active$/)).not.toBeInTheDocument();
    });
  });

  it("renders an active-alert card for a recent event", async () => {
    renderPage();
    await waitFor(() => {
      const matches = screen
        .queryAllByTestId(/^alert-active-/)
        .filter((el) => el.getAttribute("data-testid")?.startsWith("alert-active-"));
      expect(matches.length).toBeGreaterThan(0);
    });
  });
});
