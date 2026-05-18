import { makeAutoObservable, runInAction } from "mobx";
import { ApiClient } from "@/lib/api/client";
import { PrefStore } from "@/stores/pref.store";
import { ShellStore } from "@/stores/shell.store";
import { EventBus } from "@/lib/events/bus";
import { createApiClients, type ApiFactoryResult } from "@/lib/api/factory";
import type { IAdminClient } from "@/lib/admin/admin-client";
import type { IAuthClient } from "@/lib/auth/client";
import type { ISignalRClient } from "@/lib/signalr/client";
import type { AdminLoginRequest } from "@/types/auth";
import type { AppError } from "@/types/errors";

/**
 * Root MobX store. Per Core Rule §4: one store per screen + a root
 * event-bus for SignalR fan-out.
 *
 * Slices:
 *  - `prefs`   (S1) — theme mode + density, mobx-persist-store backed.
 *  - `auth`    (S3) — real cookie-mode flows via AuthClient.
 *  - `shell`   (S2) — header globals (alert count, connection status).
 *  - `bus`     (S3) — typed event-bus the SignalR client publishes into.
 *  - `signalR` (S3) — hub client; bus subscriber feeds shell.connection.
 *  - `api`     singleton (shared by every screen store).
 *  - `mode`    "real" | "mock", set once by the factory.
 *
 * Screen stores will be added in S4-S10, each registering its bus
 * subscriptions in its constructor and cleaning them in `dispose()`.
 */
export class RootStore {
  readonly prefs: PrefStore;
  readonly api: ApiClient;
  readonly auth: AuthStore;
  readonly shell: ShellStore;
  readonly bus: EventBus;
  readonly signalR: ISignalRClient;
  readonly admin: IAdminClient;
  readonly mode: "real" | "mock";

  /** Disposers owned by the root; called on app teardown. */
  private readonly disposers: Array<() => void> = [];

  constructor(clients?: ApiFactoryResult) {
    this.prefs = new PrefStore();
    this.shell = new ShellStore();
    this.bus = new EventBus();

    // Tests inject a pre-built ApiFactoryResult (or pass nothing and
    // let the factory read the env). Production uses the latter.
    const built = clients ?? createApiClients({ bus: this.bus });
    this.mode = built.mode;
    this.api = built.api;
    this.signalR = built.signalR;
    this.admin = built.admin;
    this.auth = new AuthStore(built.auth);

    // Wire SignalR connection state → shell store.
    this.disposers.push(
      this.bus.on("ConnectionStateChanged", ({ status }) =>
        this.shell.setConnection(status)
      )
    );
  }

  /** Tear down bus subscriptions + close the SignalR connection.
   *  Called on app unmount / test cleanup. Core Rule §6 cleanup. */
  async dispose(): Promise<void> {
    for (const off of this.disposers) off();
    this.disposers.length = 0;
    try {
      await this.signalR.stop();
    } catch {
      /* swallow — best-effort teardown */
    }
  }
}

/**
 * Auth slice (S3) — full real-cookie flow:
 *   hydrate()           → GET  /api/admin/auth/me
 *   signIn(creds)       → POST /api/admin/auth/login
 *   signOut()           → POST /api/admin/auth/logout
 *
 * `status` transitions:
 *   idle → loading → authenticated | anonymous | error
 *
 * 401 from the underlying ApiClient is NOT an error — it's the
 * canonical anonymous state. The client also redirects 401 to
 * /login (Core Rule §13), and this store mirrors that state.
 */
export class AuthStore {
  status: "idle" | "loading" | "authenticated" | "anonymous" | "error" = "idle";
  user: { name: string } | null = null;
  lastError: string | null = null;
  setupRequired = false;
  enabled = true;

  constructor(private readonly client: IAuthClient) {
    makeAutoObservable(this, {}, { autoBind: true });
  }

  get isAuthenticated() {
    return this.status === "authenticated";
  }

  /**
   * S3-audit M3 fix: idempotent hydrate. Concurrent / repeat calls
   * (React StrictMode double-mount) share the same in-flight
   * promise instead of racing two `GET /me` requests.
   */
  hydrate(): Promise<void> {
    const existing = inflightHydrate.get(this);
    if (existing) return existing;
    const p = (async () => {
      this.status = "loading";
      try {
        const res = await this.client.me();
        this.applyStatus(res);
      } catch (err) {
        this.applySessionError(err);
      } finally {
        inflightHydrate.delete(this);
      }
    })();
    inflightHydrate.set(this, p);
    return p;
  }

  async signIn(creds: AdminLoginRequest): Promise<boolean> {
    this.status = "loading";
    this.lastError = null;
    try {
      const res = await this.client.login(creds);
      this.applyStatus(res);
      return this.isAuthenticated;
    } catch (err) {
      // S3-audit M2 fix: login errors are user-facing. A 401 on
      // /login means invalid_credentials, NOT a session-expired
      // anonymous flip. Surface the message via lastError so the
      // form renders something the operator can act on.
      this.applyLoginError(err);
      return false;
    }
  }

  async signOut(): Promise<void> {
    this.status = "loading";
    try {
      const res = await this.client.logout();
      this.applyStatus(res);
    } catch (err) {
      // S3-audit H2 fix: logout MUST clear the local user even
      // when the network call fails. We then surface the error
      // as lastError for visibility — but the user is signed out
      // locally either way.
      runInAction(() => {
        const ae = err as AppError | undefined;
        const message = typeof ae?.message === "string" ? ae.message : "Unknown error";
        this.status = "anonymous";
        this.user = null;
        this.lastError = message;
      });
    }
  }

  /**
   * Synchronous flip — used only by integration / shell tests that
   * don't need the full async login flow. Not part of the production
   * happy path; equivalent to `signIn()` succeeding.
   */
  forceAuthenticatedForTests(name: string) {
    runInAction(() => {
      this.status = "authenticated";
      this.user = { name };
      this.lastError = null;
    });
  }

  private applyStatus(res: {
    authenticated: boolean;
    username: string;
    setupRequired: boolean;
    enabled: boolean;
  }) {
    runInAction(() => {
      this.setupRequired = res.setupRequired;
      this.enabled = res.enabled;
      if (res.authenticated) {
        this.status = "authenticated";
        this.user = { name: res.username || "operator" };
      } else {
        this.status = "anonymous";
        this.user = null;
      }
      this.lastError = null;
    });
  }

  /**
   * Session-error handler (hydrate path). 401 here means "no
   * cookie / session expired" — that's the canonical anonymous
   * state, not a user-facing error.
   */
  private applySessionError(err: unknown) {
    runInAction(() => {
      const ae = err as AppError | undefined;
      const message = typeof ae?.message === "string" ? ae.message : "Unknown error";
      if (ae?.status === 401 || ae?.category === "Unauthorized") {
        this.status = "anonymous";
        this.user = null;
        this.lastError = null;
        return;
      }
      this.status = "error";
      this.lastError = message;
    });
  }

  /**
   * Login-error handler (signIn path) — 401 here means
   * `invalid_credentials` per AdminAuthController.cs. We surface
   * the message to the login form so the operator sees feedback.
   */
  private applyLoginError(err: unknown) {
    runInAction(() => {
      const ae = err as AppError | undefined;
      const message = typeof ae?.message === "string" ? ae.message : "Unknown error";
      this.status = "anonymous";
      this.user = null;
      this.lastError = message;
    });
  }
}

/** Tracks the in-flight hydrate promise per AuthStore instance so a
 *  concurrent / repeat hydrate (StrictMode double-mount) shares the
 *  same promise. S3-audit M3 fix. Stored outside the MobX
 *  observable graph. */
const inflightHydrate = new WeakMap<AuthStore, Promise<void>>();
