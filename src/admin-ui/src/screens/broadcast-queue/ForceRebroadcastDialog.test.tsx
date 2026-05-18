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
      expect(broadcastSpy).toHaveBeenCalledTimes(1);
    });
    expect(broadcastSpy.mock.calls[0][0]).toBe(VALID_HEX);
    // S6-audit H3: second arg is an AbortSignal — verify shape only.
    expect(broadcastSpy.mock.calls[0][1]).toBeInstanceOf(AbortSignal);
    await screen.findByText(/Backend accepted the request/i);
  });

  it("blocks Cancel/Close while a broadcast POST is in flight (S6-audit H3)", async () => {
    const prefs = new PrefStore();
    const admin = new MockAdminClient();
    let releaseBroadcast: (v: { txId: string; state: string; createdAtMs: number; failReason: string | null }) => void = () => {};
    const pending = new Promise<{ txId: string; state: string; createdAtMs: number; failReason: string | null }>((resolve) => {
      releaseBroadcast = resolve;
    });
    vi.spyOn(admin, "broadcastRaw").mockReturnValueOnce(pending);

    const onClose = vi.fn();
    render(
      <ThemeProvider prefs={prefs}>
        <ForceRebroadcastDialog admin={admin} open={true} onClose={onClose} />
      </ThemeProvider>
    );

    fireEvent.change(screen.getByLabelText(/rawHex/i), { target: { value: VALID_HEX } });
    fireEvent.click(screen.getByRole("button", { name: /rebroadcast…/i }));
    fireEvent.click(await screen.findByRole("button", { name: /confirm broadcast/i }));

    // Submit is in flight — Cancel must be disabled and a click must
    // not invoke onClose.
    const cancel = await screen.findByRole("button", { name: /cancel/i });
    expect(cancel).toBeDisabled();
    fireEvent.click(cancel);
    expect(onClose).not.toHaveBeenCalled();

    // Once the POST resolves, the dialog returns to a usable state.
    releaseBroadcast({
      txId: "a".repeat(64),
      state: "Validated",
      createdAtMs: 1_000,
      failReason: null,
    });
    await screen.findByText(/Backend accepted the request/i);
    expect(screen.getByRole("button", { name: /close/i })).not.toBeDisabled();
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
