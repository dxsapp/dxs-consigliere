import { act, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { describe, expect, it } from "vitest";
import { DashboardPage } from "./DashboardPage";
import { ThemeProvider } from "@/app/ThemeProvider";
import { EventBus } from "@/lib/events/bus";
import { MockAdminClient } from "@/lib/mock/admin";
import { PrefStore } from "@/stores/pref.store";

function renderDashboard() {
  const prefs = new PrefStore();
  const admin = new MockAdminClient();
  const bus = new EventBus();
  return {
    bus,
    admin,
    ...render(
      <ThemeProvider prefs={prefs}>
        <MemoryRouter initialEntries={["/dashboard"]}>
          <DashboardPage admin={admin} bus={bus} />
        </MemoryRouter>
      </ThemeProvider>
    ),
  };
}

describe("DashboardPage (S4 smoke)", () => {
  it("renders the hero with the system-healthy verdict against mock data", async () => {
    renderDashboard();
    await waitFor(() => {
      expect(screen.getByText(/system healthy/i)).toBeInTheDocument();
    });
    expect(screen.getByText(/Pool 8\/8/)).toBeInTheDocument();
  });

  it("renders the quick-lookup search prompt + activity panels", async () => {
    renderDashboard();
    await waitFor(() => {
      expect(screen.getByText(/quick lookup/i)).toBeInTheDocument();
    });
    expect(screen.getByPlaceholderText(/search tx \/ address \/ token \/ block/i)).toBeInTheDocument();
    expect(screen.getByText(/recent broadcasts/i)).toBeInTheDocument();
    expect(screen.getByText(/source visibility/i)).toBeInTheDocument();
  });

  it("renders the per-source rates after metrics load", async () => {
    renderDashboard();
    await waitFor(() => {
      expect(screen.getByText(/^BSV P2P$/)).toBeInTheDocument();
      expect(screen.getByText(/^Bitails$/)).toBeInTheDocument();
      expect(screen.getByText(/^JungleBus$/)).toBeInTheDocument();
    });
  });

  it("renders a live broadcast row when the bus emits OnBroadcastStateChanged", async () => {
    const { bus } = renderDashboard();
    act(() => {
      bus.emit("OnBroadcastStateChanged", {
        txId: "aabbccdd11223344556677889900aabbccdd11223344556677889900aabbccdd",
        state: "PeerRelayed",
        updatedAtMs: Date.now(),
        failReason: null,
      });
    });
    await waitFor(() => {
      expect(screen.getByText(/PeerRelayed/)).toBeInTheDocument();
    });
  });
});
