import { makeAutoObservable } from "mobx";
import {
  configurePersistable,
  makePersistable,
  type StorageController,
} from "mobx-persist-store";
import type { Density, ThemeMode } from "@/app/theme";

/**
 * User preferences: theme mode + density. Persisted via
 * mobx-persist-store with a `persistVersion` guard (A1 L1):
 * an unknown / older snapshot resets to defaults rather than
 * importing potentially-invalid data after a token rename.
 *
 * The current persisted shape lives at the version below.
 * When tokens are renamed in a future iteration, bump this and
 * the upgrade is automatic-reset-to-default for legacy users.
 */
export const PREF_PERSIST_VERSION = 1;

/** Storage key (localStorage). Versioning baked into the key so
 *  legacy snapshots never collide with current ones. */
export const PREF_STORAGE_KEY = `consigliere-admin/prefs/v${PREF_PERSIST_VERSION}`;

// Configure mobx-persist-store storage adapter. Browser-only;
// the test harness substitutes an in-memory adapter.
const isBrowser = typeof window !== "undefined" && typeof window.localStorage !== "undefined";
configurePersistable({
  storage: isBrowser ? window.localStorage : undefinedStorageStub(),
  expireIn: 0, // no TTL — explicit reset on version change only
});

export class PrefStore {
  mode: ThemeMode = systemPreferredMode();
  density: Density = "comfortable";

  /** Hydration state — UI can skip rendering until persisted prefs
   *  are loaded to avoid a flash-of-default-theme. */
  hydrated = false;

  constructor() {
    makeAutoObservable(this, {}, { autoBind: true });
  }

  setMode(mode: ThemeMode) {
    this.mode = mode;
  }

  setDensity(density: Density) {
    this.density = density;
  }

  toggleMode() {
    this.mode = this.mode === "dark" ? "light" : "dark";
  }

  toggleDensity() {
    this.density = this.density === "comfortable" ? "dense" : "comfortable";
  }

  /** Wipe local prefs back to defaults. Used by the version-mismatch
   *  recovery path, the storage-failure recovery path in
   *  hydratePrefStore, and the test suite. */
  reset() {
    this.mode = systemPreferredMode();
    this.density = "comfortable";
  }

  markHydrated() {
    this.hydrated = true;
  }
}

/** Tracks the in-flight hydrate promise per store instance so a
 *  concurrent or repeat hydrate (StrictMode double-mount) shares the
 *  same promise instead of double-wiring mobx-persist-store against
 *  the same store. Stored outside the MobX observable graph so it
 *  doesn't leak into reactions. */
const inflightHydrate = new WeakMap<PrefStore, Promise<void>>();

/**
 * Activate persistence for a store. Idempotent per store: a
 * concurrent or repeat call returns the SAME promise (matters
 * under React StrictMode, which double-fires `useEffect`).
 *
 * Failure-safe: a storage / persistence error logs at warn and
 * resets the store to defaults before marking it hydrated, so the
 * UI never deadlocks rendering `null`.
 */
export function hydratePrefStore(store: PrefStore): Promise<void> {
  const existing = inflightHydrate.get(store);
  if (existing) return existing;

  const promise = (async () => {
    try {
      await makePersistable(store, {
        name: PREF_STORAGE_KEY,
        properties: ["mode", "density"],
        storage: resolveStorage(),
      });
    } catch (err) {
      // Storage / persistence failure must NOT leave the app blank.
      // Reset to defaults and let the UI render with no persisted
      // choice.
      // eslint-disable-next-line no-console
      console.warn("[PrefStore] hydrate failed; resetting to defaults", err);
      store.reset();
    } finally {
      store.markHydrated();
    }
  })();
  inflightHydrate.set(store, promise);
  return promise;
}

/** Resolve the configured storage adapter or a no-op fallback. */
function resolveStorage(): StorageController | undefined {
  if (isBrowser) return window.localStorage as unknown as StorageController;
  return undefinedStorageStub();
}

/** No-op storage adapter for non-browser environments (SSR / tests). */
function undefinedStorageStub(): StorageController {
  const store = new Map<string, string>();
  return {
    getItem: (k: string) => store.get(k) ?? null,
    setItem: (k: string, v: string) => {
      store.set(k, v);
    },
    removeItem: (k: string) => {
      store.delete(k);
    },
  };
}

/** OS-level dark-mode preference; default dark when no media-query
 *  support (always-on monitor bias per the design brief). */
function systemPreferredMode(): ThemeMode {
  if (typeof window === "undefined" || typeof window.matchMedia === "undefined") return "dark";
  return window.matchMedia("(prefers-color-scheme: light)").matches ? "light" : "dark";
}
