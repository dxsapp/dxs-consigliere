import { render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { describe, expect, it, vi } from "vitest";
import { TransactionDetailPage } from "./TransactionDetailPage";
import { ThemeProvider } from "@/app/ThemeProvider";
import { EventBus } from "@/lib/events/bus";
import { PrefStore } from "@/stores/pref.store";

const TXID = "aabbccdd11223344556677889900aabbccdd11223344556677889900aabbccdd";

function renderAt() {
  const prefs = new PrefStore();
  const bus = new EventBus();
  const signalR = { subscribeToBroadcast: vi.fn().mockResolvedValue(undefined) };
  return {
    bus,
    signalR,
    ...render(
      <ThemeProvider prefs={prefs}>
        <MemoryRouter initialEntries={[`/transactions/${TXID}`]}>
          <Routes>
            <Route
              path="/transactions/:txid"
              element={<TransactionDetailPage bus={bus} signalR={signalR} />}
            />
          </Routes>
        </MemoryRouter>
      </ThemeProvider>
    ),
  };
}

describe("TransactionDetailPage", () => {
  it("renders the txid + 5-stage timeline", async () => {
    renderAt();
    await waitFor(() => {
      expect(screen.getByText(TXID)).toBeInTheDocument();
    });
    expect(screen.getByTestId("entity-timeline")).toBeInTheDocument();
    expect(screen.getByTestId("timeline-stage-Validated")).toBeInTheDocument();
    expect(screen.getByTestId("timeline-stage-Confirmed")).toBeInTheDocument();
  });

  it("advances stages as the bus emits matching events", async () => {
    const { bus } = renderAt();
    bus.emit("OnBroadcastStateChanged", {
      txId: TXID,
      state: "PeerRelayed",
      updatedAtMs: 5_000,
      failReason: null,
    });
    await waitFor(() => {
      const stage = screen.getByTestId("timeline-stage-PeerRelayed");
      expect(stage.getAttribute("data-status")).toBe("active");
    });
    expect(screen.getAllByText(/PeerRelayed/).length).toBeGreaterThan(0);
  });
});
