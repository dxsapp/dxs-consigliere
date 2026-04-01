import { makeAutoObservable, runInAction } from "mobx";
import { authApi, ApiResponseError } from "@/api/client";
import type { AuthStatus } from "@/types/api";

type InitState = "idle" | "loading" | "ready" | "error";

export class AuthStore {
  private _status: AuthStatus | null = null;
  private _initState: InitState = "idle";
  private _loginError: string | null = null;

  constructor() {
    makeAutoObservable(this);
  }

  // ─── Derived ──────────────────────────────────────────────────────────────

  get isAuthenticated(): boolean {
    if (this._status?.setupRequired) return false;
    return this._status?.authenticated ?? false;
  }

  get isAuthEnabled(): boolean {
    return this._status?.enabled ?? true;
  }

  get setupRequired(): boolean {
    return this._status?.setupRequired ?? false;
  }

  get username(): string | undefined {
    return this._status?.username;
  }

  get initState(): InitState {
    return this._initState;
  }

  get isInitializing(): boolean {
    return this._initState === "idle" || this._initState === "loading";
  }

  get loginError(): string | null {
    return this._loginError;
  }

  // ─── Actions ──────────────────────────────────────────────────────────────

  async initialize(): Promise<void> {
    if (this._initState === "loading" || this._initState === "ready") return;

    runInAction(() => {
      this._initState = "loading";
    });

    try {
      const status = await authApi.me();
      runInAction(() => {
        this._status = status;
        this._initState = "ready";
      });
    } catch {
      runInAction(() => {
        this._initState = "error";
      });
    }
  }

  async refresh(): Promise<void> {
    runInAction(() => {
      this._initState = "loading";
    });

    try {
      const status = await authApi.me();
      runInAction(() => {
        this._status = status;
        this._initState = "ready";
        this._loginError = null;
      });
    } catch {
      runInAction(() => {
        this._initState = "error";
      });
    }
  }

  async login(username: string, password: string): Promise<boolean> {
    runInAction(() => {
      this._loginError = null;
    });

    try {
      const status = await authApi.login({ username, password });
      runInAction(() => {
        this._status = status;
      });
      return true;
    } catch (err) {
      runInAction(() => {
        if (err instanceof ApiResponseError && err.status === 401) {
          this._loginError = "Invalid username or password.";
        } else {
          this._loginError = "Login failed. Please try again.";
        }
      });
      return false;
    }
  }

  async logout(): Promise<void> {
    try {
      const status = await authApi.logout();
      runInAction(() => {
        this._status = status;
      });
    } catch {
      // best-effort; reset local state regardless
      runInAction(() => {
        this._status = null;
        this._initState = "idle";
      });
    }
  }

  clearLoginError(): void {
    this._loginError = null;
  }
}

export const authStore = new AuthStore();
