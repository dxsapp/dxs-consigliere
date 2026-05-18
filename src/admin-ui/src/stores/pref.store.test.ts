import { describe, expect, it } from "vitest";
import { PrefStore, PREF_PERSIST_VERSION, PREF_STORAGE_KEY } from "./pref.store";

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

  it("storage key is versioned so legacy snapshots are isolated (A1 L1 persistVersion)", () => {
    expect(PREF_STORAGE_KEY).toContain(`/v${PREF_PERSIST_VERSION}`);
    expect(PREF_PERSIST_VERSION).toBe(1);
  });

  it("hydrated starts false until hydratePrefStore resolves", () => {
    const s = new PrefStore();
    expect(s.hydrated).toBe(false);
    s.markHydrated();
    expect(s.hydrated).toBe(true);
  });
});
