import { makeAutoObservable } from "mobx";
import { ApiClient } from "@/lib/api/client";
import { PrefStore } from "@/stores/pref.store";

/**
 * Root MobX store. Per Core Rule §4: one store per screen + a root
 * event-bus for SignalR fan-out.
 *
 * S1 adds the user-preferences slice (theme mode + density).
 *
 * Slices:
 *  - `prefs` (S1) — theme mode + density, mobx-persist-store backed
 *  - `auth`  (S0 placeholder; full flows land in S3)
 *  - `api`   singleton (shared by every screen store)
 *  - `bus`   event-bus surface (real typed pub/sub lands in S3)
 *
 * Screen stores will be added in S4-S10, each registering its bus
 * subscriptions in its constructor and cleaning them in `dispose()`.
 */
export class RootStore {
  readonly prefs: PrefStore;
  readonly api: ApiClient;
  readonly auth: AuthStore;

  constructor() {
    this.prefs = new PrefStore();
    this.api = new ApiClient();
    this.auth = new AuthStore(this.api);
  }
}

/**
 * Auth slice — S2 synthetic. S3 wires:
 *   GET /api/admin/auth/me     → hydrate session
 *   POST /api/admin/auth/login → mutate session
 *   POST /api/admin/auth/logout
 *
 * For S2 the status transitions are operator-driven by the login
 * form / logout button so the route guard + shell can be built and
 * tested without backend coupling. `signInSynthetic` and
 * `signOutSynthetic` are the seams S3 will replace with real
 * fetches.
 */
export class AuthStore {
  status: "idle" | "loading" | "authenticated" | "anonymous" | "error" = "anonymous";
  user: { name: string } | null = null;
  lastError: string | null = null;

  constructor(_api: ApiClient) {
    void _api;
    makeAutoObservable(this, {}, { autoBind: true });
  }

  get isAuthenticated() {
    return this.status === "authenticated";
  }

  /** S2 placeholder for POST /api/admin/auth/login. */
  signInSynthetic(name: string) {
    this.status = "authenticated";
    this.user = { name: name || "operator" };
    this.lastError = null;
  }

  /** S2 placeholder for POST /api/admin/auth/logout. */
  signOutSynthetic() {
    this.status = "anonymous";
    this.user = null;
    this.lastError = null;
  }
}
