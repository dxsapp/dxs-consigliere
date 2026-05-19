import { describe, expect, it, vi } from "vitest";
import { SetupWizardStore } from "./setup-wizard.store";
import { MockAdminClient } from "@/lib/mock/admin";

function fillValidForm(store: SetupWizardStore) {
  store.setAdminField("username", "operator-a2");
  store.setAdminField("password", "ConsigliereA2!");
  store.setAdminField("confirmPassword", "ConsigliereA2!");
  // Providers + blockSync pre-filled by start() from mock options.
  store.setBlockSyncField("blockSubscriptionId", "smoke-test-block-sub");
}

describe("SetupWizardStore", () => {
  it("hydrates options + pre-fills providers + blockSync from mock", async () => {
    const store = new SetupWizardStore({ admin: new MockAdminClient() });
    await store.start();
    expect(store.status).toBe("ready");
    expect(store.options?.allowed.bitailsTransports).toContain("websocket");
    expect(store.providers.bitailsBaseUrl).toBe("https://api.bitails.io");
    expect(store.blockSync.baseUrl).toBe("https://junglebus.gorillapool.io");
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

  it("rejects URLs that are not http(s)", async () => {
    const store = new SetupWizardStore({ admin: new MockAdminClient() });
    await store.start();
    store.setProvidersField("bitailsBaseUrl", "ftp://bad");
    expect(
      store.errorsForStep(2).some((e) => e.field === "providers.bitailsBaseUrl")
    ).toBe(true);
    store.setProvidersField("bitailsBaseUrl", "https://api.bitails.io");
    expect(
      store.errorsForStep(2).some((e) => e.field === "providers.bitailsBaseUrl")
    ).toBe(false);
    store.dispose();
  });

  it("requires the JungleBus block subscription ID (mirrors backend rule)", async () => {
    const store = new SetupWizardStore({ admin: new MockAdminClient() });
    await store.start();
    expect(
      store.errorsForStep(3).some((e) => e.field === "blockSync.blockSubscriptionId")
    ).toBe(true);
    store.setBlockSyncField("blockSubscriptionId", "sub-1");
    expect(
      store.errorsForStep(3).some((e) => e.field === "blockSync.blockSubscriptionId")
    ).toBe(false);
    store.dispose();
  });

  it("goNext refuses to advance when the current step is invalid", async () => {
    const store = new SetupWizardStore({ admin: new MockAdminClient() });
    await store.start();
    store.goNext();
    expect(store.step).toBe(1); // blocked by admin form errors
    fillValidForm(store);
    store.goNext();
    expect(store.step).toBe(2);
    store.goNext();
    expect(store.step).toBe(3);
    store.goNext();
    expect(store.step).toBe(4);
    store.dispose();
  });

  it("submit posts the complete request + flips status to submitted", async () => {
    const admin = new MockAdminClient();
    const spy = vi.spyOn(admin, "completeSetup");
    const store = new SetupWizardStore({ admin });
    await store.start();
    fillValidForm(store);
    store.jumpTo(4);
    await store.submit();
    expect(spy).toHaveBeenCalledTimes(1);
    const req = spy.mock.calls[0][0];
    expect(req.admin.username).toBe("operator-a2");
    expect(req.blockSync.blockSubscriptionId).toBe("smoke-test-block-sub");
    expect(req.providers.junglebus.blockSubscriptionId).toBe("smoke-test-block-sub");
    expect(store.status).toBe("submitted");
    expect(store.submittedStatus?.setupCompleted).toBe(true);
    store.dispose();
  });

  it("submit captures backend errors without flipping to submitted", async () => {
    const admin = new MockAdminClient();
    vi.spyOn(admin, "completeSetup").mockRejectedValueOnce(new Error("boom"));
    const store = new SetupWizardStore({ admin });
    await store.start();
    fillValidForm(store);
    store.jumpTo(4);
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

  it("canSubmit is false until everything passes + step is 4", async () => {
    const store = new SetupWizardStore({ admin: new MockAdminClient() });
    await store.start();
    expect(store.canSubmit).toBe(false);
    fillValidForm(store);
    expect(store.canSubmit).toBe(false); // wrong step
    store.jumpTo(4);
    expect(store.canSubmit).toBe(true);
    store.dispose();
  });
});
