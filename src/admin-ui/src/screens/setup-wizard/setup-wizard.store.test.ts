import { describe, expect, it, vi } from "vitest";
import { SetupWizardStore } from "./setup-wizard.store";
import { MockAdminClient } from "@/lib/mock/admin";

function fillValidAdmin(store: SetupWizardStore) {
  store.setAdminField("username", "operator-a2");
  store.setAdminField("password", "ConsigliereA2!");
  store.setAdminField("confirmPassword", "ConsigliereA2!");
}

describe("SetupWizardStore", () => {
  it("hydrates options on start", async () => {
    const store = new SetupWizardStore({ admin: new MockAdminClient() });
    await store.start();
    expect(store.status).toBe("ready");
    expect(store.step).toBe(1);
    expect(store.options?.allowed.bitailsTransports).toContain("websocket");
    store.dispose();
  });

  it("flags admin errors until username + matching passwords are set", async () => {
    const store = new SetupWizardStore({ admin: new MockAdminClient() });
    await store.start();
    expect(store.errorsForStep(1).length).toBeGreaterThan(0);
    store.setAdminField("username", "ab"); // too short
    expect(store.errorsForStep(1).some((e) => e.field === "admin.username")).toBe(true);
    store.setAdminField("username", "admin");
    store.setAdminField("password", "short");
    expect(store.errorsForStep(1).some((e) => e.field === "admin.password")).toBe(true);
    store.setAdminField("password", "longenough");
    store.setAdminField("confirmPassword", "different");
    expect(store.errorsForStep(1).some((e) => e.field === "admin.confirmPassword")).toBe(true);
    store.setAdminField("confirmPassword", "longenough");
    expect(store.errorsForStep(1)).toHaveLength(0);
    store.dispose();
  });

  it("canSubmit is false until the admin form is valid (no provider/block-sync gating)", async () => {
    const store = new SetupWizardStore({ admin: new MockAdminClient() });
    expect(store.canSubmit).toBe(false); // not ready yet
    await store.start();
    expect(store.canSubmit).toBe(false); // admin form empty
    fillValidAdmin(store);
    // No providers or block-sync input is required — admin alone unlocks completion.
    expect(store.canSubmit).toBe(true);
    store.dispose();
  });

  it("submit posts an admin-only request (empty providers/blockSync) + flips to submitted", async () => {
    const admin = new MockAdminClient();
    const spy = vi.spyOn(admin, "completeSetup");
    const store = new SetupWizardStore({ admin });
    await store.start();
    fillValidAdmin(store);
    await store.submit();
    expect(spy).toHaveBeenCalledTimes(1);
    const req = spy.mock.calls[0][0];
    expect(req.admin.enabled).toBe(true);
    expect(req.admin.username).toBe("operator-a2");
    // Providers + block sync are sent empty so the backend keeps the
    // seeded p2p-primary defaults — no subscription required.
    expect(req.providers.realtimePrimaryProvider).toBe("");
    expect(req.providers.rawTxPrimaryProvider).toBe("");
    expect(req.providers.bitailsTransport).toBe("");
    expect(req.blockSync.baseUrl).toBe("");
    expect(req.blockSync.blockSubscriptionId).toBe("");
    expect(store.status).toBe("submitted");
    expect(store.submittedStatus?.setupCompleted).toBe(true);
    store.dispose();
  });

  it("submit is a no-op while the admin form is invalid", async () => {
    const admin = new MockAdminClient();
    const spy = vi.spyOn(admin, "completeSetup");
    const store = new SetupWizardStore({ admin });
    await store.start();
    await store.submit(); // admin form empty → blocked
    expect(spy).not.toHaveBeenCalled();
    expect(store.status).toBe("ready");
    store.dispose();
  });

  it("submit captures backend errors without flipping to submitted", async () => {
    const admin = new MockAdminClient();
    vi.spyOn(admin, "completeSetup").mockRejectedValueOnce(new Error("boom"));
    const store = new SetupWizardStore({ admin });
    await store.start();
    fillValidAdmin(store);
    await store.submit();
    expect(store.status).toBe("ready");
    expect(store.error).toBe("boom");
    store.dispose();
  });

  it("dispose aborts in-flight fetch + ignores late resolution", async () => {
    const signalRef: { current: AbortSignal | null } = { current: null };
    const admin = new MockAdminClient();
    vi.spyOn(admin, "getSetupOptions").mockImplementation(
      (sig?: AbortSignal) => {
        signalRef.current = sig ?? null;
        return new Promise(() => {
          /* never resolves */
        });
      }
    );
    const store = new SetupWizardStore({ admin });
    void store.start();
    store.dispose();
    expect(signalRef.current?.aborted).toBe(true);
  });
});
