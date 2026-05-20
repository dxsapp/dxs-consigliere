import { makeAutoObservable, runInAction } from "mobx";
import { sanitize } from "@/screens/logs/sanitizer";

/**
 * wave-A3 S4 — admin live-log tail backed by SignalR
 * `/ws/logs`. The store owns its own HubConnection (the global
 * SignalR client is hard-wired to `/ws/consigliere`); the
 * LogsPage mount creates + starts the connection and the
 * unmount tears it down. Per the slice contract there is no
 * cross-reconnect durability — a backend restart starts the
 * client buffer fresh.
 */
export interface LogEventDto {
  unixMs: number;
  level: string;
  category: string;
  message: string;
  exception: string | null;
}

export type LogStreamStatus = "idle" | "connecting" | "live" | "error" | "stopped";

export interface LogsStoreOptions {
  hubUrl?: string;
  /** Visible buffer cap. Default 1000. */
  capacity?: number;
  /** Test seam — pre-built HubConnection-like surface. Production
   *  uses the lazy-imported `@microsoft/signalr` SDK. */
  buildConnection?: (hubUrl: string) => Promise<HubLike>;
}

interface HubLike {
  start(): Promise<void>;
  stop(): Promise<void>;
  invoke(method: string, ...args: unknown[]): Promise<unknown>;
  on(event: string, handler: (...args: unknown[]) => void): void;
  onclose(handler: (err?: unknown) => void): void;
}

const DEFAULT_HUB_URL = "/ws/logs";
const DEFAULT_CAPACITY = 1000;

export class LogsStore {
  entries: LogEventDto[] = [];
  status: LogStreamStatus = "idle";
  error: string | null = null;
  filter: { minLevel: string; category: string } = { minLevel: "information", category: "" };

  private readonly hubUrl: string;
  private readonly capacity: number;
  private readonly builder: (hubUrl: string) => Promise<HubLike>;
  private connection: HubLike | null = null;

  constructor(opts: LogsStoreOptions = {}) {
    this.hubUrl = opts.hubUrl ?? DEFAULT_HUB_URL;
    this.capacity = opts.capacity ?? DEFAULT_CAPACITY;
    this.builder = opts.buildConnection ?? defaultBuildConnection;
    makeAutoObservable(this, {}, { autoBind: true });
  }

  setMinLevel(level: string): void {
    this.filter = { ...this.filter, minLevel: level };
  }

  setCategoryFilter(value: string): void {
    this.filter = { ...this.filter, category: value };
  }

  async start(): Promise<void> {
    if (this.connection) return;
    this.status = "connecting";
    this.error = null;
    try {
      const conn = await this.builder(this.hubUrl);
      conn.on("OnLogEvent", (raw: unknown) => this.handle(raw as LogEventDto));
      conn.onclose((err?: unknown) => {
        runInAction(() => {
          this.status = "stopped";
          if (err) this.error = describeError(err);
        });
      });
      await conn.start();
      this.connection = conn;
      await conn.invoke(
        "SubscribeToLogs",
        this.filter.minLevel || undefined,
        this.filter.category || undefined,
      );
      runInAction(() => {
        this.status = "live";
      });
    } catch (err) {
      runInAction(() => {
        this.status = "error";
        this.error = describeError(err);
      });
    }
  }

  async resubscribe(): Promise<void> {
    if (!this.connection) {
      await this.start();
      return;
    }
    // S4-audit M1 fix: clear BEFORE the invoke, not after. The
    // hub's `SubscribeToLogs` flushes the ring-buffer snapshot
    // via fire-and-forget `SendAsync` BEFORE returning, so by
    // the time `await invoke(...)` resolves the new snapshot
    // frames may already have populated `this.entries` via
    // `handle(...)`. Clearing afterwards would wipe them and
    // leave the operator staring at an empty grid until the
    // next live emission landed. Clearing first means the new
    // snapshot is the only content the operator sees with the
    // new filters applied.
    runInAction(() => {
      this.entries = [];
    });
    await this.connection.invoke(
      "SubscribeToLogs",
      this.filter.minLevel || undefined,
      this.filter.category || undefined,
    );
  }

  async stop(): Promise<void> {
    if (!this.connection) return;
    try { await this.connection.invoke("UnsubscribeFromLogs"); } catch { /* swallow */ }
    try { await this.connection.stop(); } catch { /* swallow */ }
    this.connection = null;
    runInAction(() => {
      this.status = "stopped";
    });
  }

  clear(): void {
    this.entries = [];
  }

  private handle(raw: LogEventDto): void {
    if (!raw || typeof raw !== "object") return;
    // Defence-in-depth: the backend sanitizes before emit, but
    // we re-apply on the client too so a future hot-path that
    // bypasses LogSanitizer can't leak secrets to the rendered
    // DOM.
    const safeMessage = sanitize(raw.message ?? "").out;
    const safeException = raw.exception ? sanitize(raw.exception).out : null;
    const entry: LogEventDto = {
      unixMs: raw.unixMs,
      level: raw.level,
      category: raw.category,
      message: safeMessage,
      exception: safeException,
    };
    runInAction(() => {
      this.entries = appendCapped(this.entries, entry, this.capacity);
    });
  }
}

function appendCapped(buf: LogEventDto[], next: LogEventDto, cap: number): LogEventDto[] {
  const out = buf.length >= cap ? buf.slice(buf.length - cap + 1) : buf.slice();
  out.push(next);
  return out;
}

async function defaultBuildConnection(hubUrl: string): Promise<HubLike> {
  const { HubConnectionBuilder, LogLevel } = await import("@microsoft/signalr");
  return new HubConnectionBuilder()
    .withUrl(hubUrl, { withCredentials: true })
    .configureLogging(LogLevel.Warning)
    .build() as unknown as HubLike;
}

function describeError(err: unknown): string {
  if (typeof err === "object" && err && "message" in err) {
    const message = (err as { message?: string }).message;
    if (typeof message === "string" && message) return message;
  }
  return "Log stream error";
}
