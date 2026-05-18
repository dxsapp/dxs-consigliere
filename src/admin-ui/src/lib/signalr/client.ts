import type { HubConnection } from "@microsoft/signalr";
import type { EventBus } from "@/lib/events/bus";

/**
 * SignalR client (Core Rule §6 cleanup + §4 event-bus fan-out).
 *
 * Wraps `@microsoft/signalr` against the backend `/wallethub`. Auto-
 * reconnects via the built-in policy + a stale-callback the shell
 * uses to grey-out widgets when the connection drops for more than
 * the configured stale-threshold.
 *
 * Per-screen subscribers consume hub events through the bus, not
 * through this client directly.
 */
export interface ISignalRClient {
  start(): Promise<void>;
  stop(): Promise<void>;
  /** Server-side subscription primitives — call once per session
   *  per screen interest. */
  subscribeToBlockTip(): Promise<void>;
  subscribeToReorg(): Promise<void>;
  subscribeToBroadcast(txId: string): Promise<void>;
}

export interface SignalRClientOptions {
  /** Hub URL relative to origin (passes through the Vite proxy in dev). */
  hubUrl: string;
  /** Threshold after which a disconnected client is treated as stale
   *  (per-widget overlay applies). Default 10 s. */
  staleAfterMs?: number;
}

export class SignalRClient implements ISignalRClient {
  private connection: HubConnection | null = null;
  private staleTimer: ReturnType<typeof setTimeout> | null = null;
  /** S3-audit M1 fix: flipped to true only after `conn.start()`
   *  resolves; flipped back on stop() or start() rejection. Avoids
   *  invoking on a half-initialised HubConnection. */
  private connected = false;
  private readonly hubUrl: string;
  private readonly staleAfterMs: number;

  constructor(
    private readonly bus: EventBus,
    options: SignalRClientOptions
  ) {
    this.hubUrl = options.hubUrl;
    this.staleAfterMs = options.staleAfterMs ?? 10_000;
  }

  /** Test seam — pins the configured hub URL against the backend
   *  WalletHub.Route constant. */
  get hubUrlForTests(): string {
    return this.hubUrl;
  }

  async start(): Promise<void> {
    if (this.connection) return;
    // S3-bundle-budget: dynamic-import pulls @microsoft/signalr
    // (~16 KB gzip) into its own route chunk so the cold-load
    // shell stays under A1 M3's 200 KB ceiling. Subsequent
    // starts hit the chunk cache.
    const { HubConnectionBuilder, LogLevel } = await import("@microsoft/signalr");

    const conn = new HubConnectionBuilder()
      .withUrl(this.hubUrl, { withCredentials: true })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    // Hub → bus fan-out.
    conn.on("OnNewBlock", (tip) => this.bus.emit("OnNewBlock", tip));
    conn.on("OnReorg", (evt) => this.bus.emit("OnReorg", evt));
    conn.on("OnBroadcastStateChanged", (evt) => this.bus.emit("OnBroadcastStateChanged", evt));

    // Connection-state → bus, with a stale-after-disconnect timer.
    conn.onreconnecting(() => {
      this.connected = false;
      this.markStaleSoon();
    });
    conn.onreconnected(() => {
      this.connected = true;
      this.markOnline();
    });
    conn.onclose(() => {
      this.connected = false;
      this.markOffline();
    });

    this.connection = conn;
    try {
      await conn.start();
      this.connected = true;
      this.markOnline();
    } catch (err) {
      // S3-audit M1 fix: failed start must null the connection so
      // subsequent start() calls can retry, AND clear `connected`
      // so requireConnected() doesn't allow invokes on the dead
      // SDK object.
      this.connected = false;
      this.connection = null;
      this.markOffline();
      throw err;
    }
  }

  async stop(): Promise<void> {
    if (!this.connection) return;
    this.clearStaleTimer();
    this.connected = false;
    try {
      await this.connection.stop();
    } finally {
      this.connection = null;
      // Final state — bus subscribers can react to disconnect.
      this.bus.emit("ConnectionStateChanged", { status: "offline" });
    }
  }

  async subscribeToBlockTip(): Promise<void> {
    await this.requireConnected().invoke("SubscribeToBlockTip");
  }

  async subscribeToReorg(): Promise<void> {
    await this.requireConnected().invoke("SubscribeToReorg");
  }

  async subscribeToBroadcast(txId: string): Promise<void> {
    await this.requireConnected().invoke("SubscribeToBroadcast", txId);
  }

  private requireConnected(): HubConnection {
    if (!this.connection || !this.connected) {
      throw new Error(
        "SignalR connection not in Connected state; call start() and await it before invoking"
      );
    }
    return this.connection;
  }

  private markOnline() {
    this.clearStaleTimer();
    this.bus.emit("ConnectionStateChanged", { status: "online" });
  }

  private markStaleSoon() {
    this.clearStaleTimer();
    this.staleTimer = setTimeout(() => {
      this.bus.emit("ConnectionStateChanged", { status: "stale" });
    }, this.staleAfterMs);
  }

  private markOffline() {
    this.clearStaleTimer();
    this.bus.emit("ConnectionStateChanged", { status: "offline" });
  }

  private clearStaleTimer() {
    if (this.staleTimer !== null) {
      clearTimeout(this.staleTimer);
      this.staleTimer = null;
    }
  }
}
