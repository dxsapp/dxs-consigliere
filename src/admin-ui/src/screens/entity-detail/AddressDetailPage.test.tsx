import { render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { describe, expect, it } from "vitest";
import { AddressDetailPage } from "./AddressDetailPage";
import { TokenDetailPage } from "./TokenDetailPage";
import { ThemeProvider } from "@/app/ThemeProvider";
import { MockAdminClient } from "@/lib/mock/admin";
import { PrefStore } from "@/stores/pref.store";

function renderAddress(path = "/addresses/1HotWalletAbcDefGhi") {
  const prefs = new PrefStore();
  const admin = new MockAdminClient();
  return render(
    <ThemeProvider prefs={prefs}>
      <MemoryRouter initialEntries={[path]}>
        <Routes>
          <Route
            path="/addresses/:address"
            element={<AddressDetailPage admin={admin} />}
          />
        </Routes>
      </MemoryRouter>
    </ThemeProvider>
  );
}

function renderToken(path = "/tokens/dstas-token-id") {
  const prefs = new PrefStore();
  const admin = new MockAdminClient();
  return render(
    <ThemeProvider prefs={prefs}>
      <MemoryRouter initialEntries={[path]}>
        <Routes>
          <Route
            path="/tokens/:tokenId"
            element={<TokenDetailPage admin={admin} />}
          />
        </Routes>
      </MemoryRouter>
    </ThemeProvider>
  );
}

describe("AddressDetailPage", () => {
  it("renders the address + readiness timeline + summary", async () => {
    renderAddress();
    // Summary renders only after the mock dynamic-import resolves;
    // wait on it rather than the always-on header.
    await waitFor(() => {
      expect(screen.getAllByText(/Balance/i).length).toBeGreaterThan(0);
    });
    expect(screen.getByText(/Tracking readiness/i)).toBeInTheDocument();
    expect(screen.getByTestId("entity-timeline")).toBeInTheDocument();
    // Mock returns a Ready readiness — first stage should be done.
    const tracked = screen.getByTestId("timeline-stage-tracked");
    expect(tracked.getAttribute("data-status")).toBe("done");
  });
});

describe("TokenDetailPage", () => {
  it("renders the tokenId + readiness timeline + protocol summary", async () => {
    renderToken();
    await waitFor(() => {
      expect(screen.getAllByText(/DSTAS/).length).toBeGreaterThan(0);
    });
    expect(screen.getByText(/Tracking readiness/i)).toBeInTheDocument();
    expect(screen.getByTestId("entity-timeline")).toBeInTheDocument();
    expect(screen.getByText(/Protocol/i)).toBeInTheDocument();
  });
});
