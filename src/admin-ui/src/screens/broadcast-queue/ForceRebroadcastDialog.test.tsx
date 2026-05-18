import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { ForceRebroadcastDialog } from "./ForceRebroadcastDialog";
import { ThemeProvider } from "@/app/ThemeProvider";
import { MockAdminClient } from "@/lib/mock/admin";
import { PrefStore } from "@/stores/pref.store";

function renderDialog(overrides: { onClose?: () => void } = {}) {
  const prefs = new PrefStore();
  const admin = new MockAdminClient();
  const onClose = overrides.onClose ?? vi.fn();
  const broadcastSpy = vi.spyOn(admin, "broadcastRaw");
  return {
    admin,
    onClose,
    broadcastSpy,
    ...render(
      <ThemeProvider prefs={prefs}>
        <ForceRebroadcastDialog admin={admin} open={true} onClose={onClose} />
      </ThemeProvider>
    ),
  };
}

const VALID_HEX = "0100000001" + "a".repeat(64);

describe("ForceRebroadcastDialog", () => {
  it("disables the primary CTA until the rawHex is plausible", () => {
    renderDialog();
    const cta = screen.getByRole("button", { name: /rebroadcast…/i });
    expect(cta).toBeDisabled();
    const input = screen.getByLabelText(/rawHex/i);
    fireEvent.change(input, { target: { value: "not hex" } });
    expect(cta).toBeDisabled();
    fireEvent.change(input, { target: { value: VALID_HEX } });
    expect(cta).not.toBeDisabled();
  });

  it("two-step confirms before POSTing the broadcast", async () => {
    const { broadcastSpy } = renderDialog();
    const input = screen.getByLabelText(/rawHex/i);
    fireEvent.change(input, { target: { value: VALID_HEX } });
    fireEvent.click(screen.getByRole("button", { name: /rebroadcast…/i }));
    // After the first click the CTA flips to "Confirm broadcast".
    const confirm = await screen.findByRole("button", { name: /confirm broadcast/i });
    fireEvent.click(confirm);
    await waitFor(() => {
      expect(broadcastSpy).toHaveBeenCalledWith(VALID_HEX);
    });
    await screen.findByText(/Backend accepted the request/i);
  });

  it("surfaces network errors as an inline Alert", async () => {
    const prefs = new PrefStore();
    const admin = new MockAdminClient();
    vi.spyOn(admin, "broadcastRaw").mockRejectedValueOnce(new Error("ECONNREFUSED"));
    render(
      <ThemeProvider prefs={prefs}>
        <ForceRebroadcastDialog admin={admin} open={true} onClose={vi.fn()} />
      </ThemeProvider>
    );
    fireEvent.change(screen.getByLabelText(/rawHex/i), { target: { value: VALID_HEX } });
    fireEvent.click(screen.getByRole("button", { name: /rebroadcast…/i }));
    fireEvent.click(await screen.findByRole("button", { name: /confirm broadcast/i }));
    await screen.findByText(/ECONNREFUSED/);
  });
});
