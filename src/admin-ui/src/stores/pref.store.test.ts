import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  PrefStore,
  PREF_PERSIST_VERSION,
  PREF_STORAGE_KEY,
  hydratePrefStore,
} from "./pref.store";

describe("PrefStore", () => {
  it("defaults to dark + comfortable when no system preference signal is present", () => {
    const s = new PrefStore();
    // jsdom's matchMedia returns matches=false, so the fallback path
    // applies → dark (per always-on-monitor bias).
    expect(s.mode).toBe("dark");
    expect(s.density).toBe("comfortable");
  });

  it("toggleMode flips between light and dark", () => {
    const s = new PrefStore();
    s.setMode("light");
    expect(s.mode).toBe("light");
    s.toggleMode();
    expect(s.mode).toBe("dark");
    s.toggleMode();
    expect(s.mode).toBe("light");
  });

  it("toggleDensity flips between comfortable and dense", () => {
    const s = new PrefStore();
    expect(s.density).toBe("comfortable");
    s.toggleDensity();
    expect(s.density).toBe("dense");
    s.toggleDensity();
    expect(s.density).toBe("comfortable");
  });

  it("reset restores defaults", () => {
    const s = new PrefStore();
    s.setMode("light");
    s.setDensity("dense");
    s.reset();
    expect(s.mode).toBe("dark");
    expect(s.density).toBe("comfortable");
  });

  it("storage key has the exact versioned shape (A1 L1 + S1-audit L2)", () => {
    // Sharper assertion than substring-match: exact-equality on the
    // full canonical key catches malformed patterns like
    // "v01" / "v 1" / extra prefix drift.
    expect(PREF_STORAGE_KEY).toBe("consigliere-admin/prefs/v1");
    expect(PREF_PERSIST_VERSION).toBe(1);
  });

  it("hydrated starts false until hydratePrefStore resolves", () => {
    const s = new PrefStore();
    expect(s.hydrated).toBe(false);
    s.markHydrated();
    expect(s.hydrated).toBe(true);
  });
});

describe("hydratePrefStore", () => {
  beforeEach(() => {
    window.localStorage.clear();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("legacy snapshot under a different key is ignored (S1-audit L2)", async () => {
    // A user upgrading from a hypothetical v0 has a snapshot under
    // the OLD key. The current store must NOT read that.
    window.localStorage.setItem(
      "consigliere-admin/prefs/v0",
      JSON.stringify({ mode: "light", density: "dense" })
    );
    // The current-version key is empty.
    expect(window.localStorage.getItem(PREF_STORAGE_KEY)).toBeNull();

    const s = new PrefStore();
    await hydratePrefStore(s);

    // Defaults preserved; the legacy snapshot didn't leak in.
    expect(s.mode).toBe("dark");
    expect(s.density).toBe("comfortable");
    expect(s.hydrated).toBe(true);
  });

  it("repeat hydrate returns the same in-flight promise (StrictMode-safe)", async () => {
    const s = new PrefStore();
    const a = hydratePrefStore(s);
    const b = hydratePrefStore(s);
    expect(a).toBe(b); // identity check: same Promise reference
    await a;
    expect(s.hydrated).toBe(true);
  });

  it("storage failure resets to defaults and still marks hydrated (S1-audit M1)", async () => {
    // Force localStorage.getItem to throw on the FIRST persistable
    // read so mobx-persist-store's makePersistable rejects.
    const original = Storage.prototype.getItem;
    Storage.prototype.getItem = function () {
      throw new Error("simulated quota / privacy-mode failure");
    };

    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
    const s = new PrefStore();
    s.setMode("light");
    s.setDensity("dense");

    await hydratePrefStore(s);

    // The store reset to defaults so the UI never deadlocks; we also
    // marked it hydrated so App.tsx renders something.
    expect(s.mode).toBe("dark");
    expect(s.density).toBe("comfortable");
    expect(s.hydrated).toBe(true);
    expect(warn).toHaveBeenCalled();

    Storage.prototype.getItem = original;
  });
});
