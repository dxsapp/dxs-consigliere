import { act, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { describe, expect, it, vi } from "vitest";
import { TransactionDetailPage } from "./TransactionDetailPage";
import { ThemeProvider } from "@/app/ThemeProvider";
import { EventBus } from "@/lib/events/bus";
import { PrefStore } from "@/stores/pref.store";

const TXID = "aabbccdd11223344556677889900aabbccdd11223344556677889900aabbccdd";

function renderAt(opts: { subscribe?: ReturnType<typeof vi.fn> } = {}) {
  const prefs = new PrefStore();
  const bus = new EventBus();
  const signalR = {
    subscribeToBroadcast:
      opts.subscribe ?? vi.fn().mockResolvedValue(undefined),
  };
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
    // S6-audit L2: bus emit triggers MobX → React state update; wrap
    // in act() so React doesn't warn about an un-flushed update.
    act(() => {
      bus.emit("OnBroadcastStateChanged", {
        txId: TXID,
        state: "PeerRelayed",
        updatedAtMs: 5_000,
        failReason: null,
      });
    });
    await waitFor(() => {
      const stage = screen.getByTestId("timeline-stage-PeerRelayed");
      expect(stage.getAttribute("data-status")).toBe("active");
    });
    expect(screen.getAllByText(/PeerRelayed/).length).toBeGreaterThan(0);
  });

  it("renders the subscribe-error Alert when the hub invoke rejects (S5-audit L2 smoke)", async () => {
    const subscribe = vi.fn().mockRejectedValue(new Error("hub disconnected"));
    renderAt({ subscribe });
    await waitFor(() => {
      expect(screen.getByText(/Live subscription not active/i)).toBeInTheDocument();
    });
    expect(screen.getByText(/hub disconnected/)).toBeInTheDocument();
  });
});
