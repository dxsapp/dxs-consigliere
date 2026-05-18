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
 * Auth slice — S0 placeholder. S3 wires:
 *   GET /api/admin/auth/me     → hydrate session
 *   POST /api/admin/auth/login → mutate session
 *   POST /api/admin/auth/logout
 */
export class AuthStore {
  status: "idle" | "loading" | "authenticated" | "anonymous" | "error" = "idle";

  constructor(_api: ApiClient) {
    void _api;
    makeAutoObservable(this);
  }
}
