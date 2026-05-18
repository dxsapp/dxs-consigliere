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
   *  recovery path and exposed for tests. */
  reset() {
    this.mode = systemPreferredMode();
    this.density = "comfortable";
  }

  markHydrated() {
    this.hydrated = true;
  }
}

/**
 * Activate persistence for a store. Separated from the constructor
 * so unit tests can choose to opt-in.
 */
export async function hydratePrefStore(store: PrefStore): Promise<void> {
  await makePersistable(store, {
    name: PREF_STORAGE_KEY,
    properties: ["mode", "density"],
    // Manual hydrate so we can guard with the version check below.
    storage: resolveStorage(),
  });
  store.markHydrated();
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
