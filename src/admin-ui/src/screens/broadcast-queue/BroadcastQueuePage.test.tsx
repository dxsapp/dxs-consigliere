import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { describe, expect, it } from "vitest";
import { BroadcastQueuePage } from "./BroadcastQueuePage";
import { ThemeProvider } from "@/app/ThemeProvider";
import { EventBus } from "@/lib/events/bus";
import { MockAdminClient } from "@/lib/mock/admin";
import { PrefStore } from "@/stores/pref.store";

function renderPage() {
  const prefs = new PrefStore();
  const bus = new EventBus();
  const admin = new MockAdminClient();
  return {
    bus,
    admin,
    ...render(
      <ThemeProvider prefs={prefs}>
        <MemoryRouter initialEntries={["/broadcast-queue"]}>
          <BroadcastQueuePage admin={admin} bus={bus} />
        </MemoryRouter>
      </ThemeProvider>
    ),
  };
}

const TXID = "a".repeat(64);

describe("BroadcastQueuePage", () => {
  it("renders three lanes with empty state and a force-rebroadcast CTA", () => {
    renderPage();
    expect(screen.getByTestId("lane-validated")).toBeInTheDocument();
    expect(screen.getByTestId("lane-dispatching")).toBeInTheDocument();
    expect(screen.getByTestId("lane-peerRelayed")).toBeInTheDocument();
    expect(
      screen.getAllByText(/no cards in this lane/i).length
    ).toBeGreaterThanOrEqual(3);
    expect(screen.getByRole("button", { name: /force rebroadcast/i })).toBeInTheDocument();
  });

  it("renders a card in the Validated lane after a matching event", async () => {
    const { bus } = renderPage();
    bus.emit("OnBroadcastStateChanged", {
      txId: TXID,
      state: "Validated",
      updatedAtMs: 1_000,
      failReason: null,
    });
    await waitFor(() => {
      expect(screen.getByTestId(`queue-card-${TXID}`)).toBeInTheDocument();
    });
    const validatedLane = screen.getByTestId("lane-validated");
    expect(within(validatedLane).getByTestId(`queue-card-${TXID}`)).toBeInTheDocument();
  });

  it("opens the force-rebroadcast dialog from the page CTA", async () => {
    renderPage();
    fireEvent.click(screen.getByRole("button", { name: /^force rebroadcast$/i }));
    await waitFor(() => {
      expect(screen.getByRole("dialog")).toBeInTheDocument();
    });
    expect(screen.getByLabelText(/rawHex/i)).toBeInTheDocument();
  });
});
