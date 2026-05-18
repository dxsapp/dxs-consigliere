import type { EventBus } from "@/lib/events/bus";
import type { ISignalRClient } from "@/lib/signalr/client";

/**
 * S3 mock SignalR client. Used when `VITE_API_MODE=mock` so the
 * UI gets believable block-tip / broadcast / reorg events without
 * a backend. Maintains the same `ISignalRClient` contract +
 * connection-state lifecycle as the real client (Core Rule §12 —
 * mock mirrors real shape).
 *
 * Cadence:
 *  - Block tip every 20 s (advances height by 1).
 *  - One broadcast-state transition every 8 s, cycling through
 *    Validated → Dispatching → PeerRelayed → Mined.
 *  - Reorg is NEVER emitted by default; tests can call
 *    `emitReorg()` to inject one.
 */
export class MockSignalRClient implements ISignalRClient {
  private blockTimer: ReturnType<typeof setInterval> | null = null;
  private broadcastTimer: ReturnType<typeof setInterval> | null = null;
  private nextHeight = 850_000;
  private cycleIndex = 0;

  constructor(private readonly bus: EventBus) {}

  async start(): Promise<void> {
    this.bus.emit("ConnectionStateChanged", { status: "online" });
    this.blockTimer = setInterval(() => this.emitBlockTip(), 20_000);
    this.broadcastTimer = setInterval(() => this.emitNextBroadcastState(), 8_000);
  }

  async stop(): Promise<void> {
    if (this.blockTimer) clearInterval(this.blockTimer);
    if (this.broadcastTimer) clearInterval(this.broadcastTimer);
    this.blockTimer = null;
    this.broadcastTimer = null;
    this.bus.emit("ConnectionStateChanged", { status: "offline" });
  }

  async subscribeToBlockTip(): Promise<void> {
    // No-op — the mock always emits block tips.
  }

  async subscribeToReorg(): Promise<void> {
    // No-op — call `emitReorg()` from a test if you want one.
  }

  async subscribeToBroadcast(_txId: string): Promise<void> {
    void _txId;
    // No-op — the mock emits a synthetic stream for any txid.
  }

  /** Test seam: drive a reorg explicitly. */
  emitReorg() {
    this.bus.emit("OnReorg", {
      commonAncestorHash: hex64("aa"),
      commonAncestorHeight: this.nextHeight - 3,
      orphanedHashes: [hex64("bb"), hex64("cc")],
      newTipHash: hex64("dd"),
      newTipHeight: this.nextHeight,
      degradedState: false,
    });
  }

  private emitBlockTip() {
    const height = this.nextHeight++;
    this.bus.emit("OnNewBlock", {
      hash: hex64((height % 256).toString(16).padStart(2, "0")),
      height,
      timestampMs: Date.now(),
      prevHash: hex64(((height - 1) % 256).toString(16).padStart(2, "0")),
      headerSize: 80,
    });
  }

  private emitNextBroadcastState() {
    const cycle = ["Validated", "Dispatching", "PeerRelayed", "Mined"];
    const state = cycle[this.cycleIndex % cycle.length];
    this.cycleIndex++;
    this.bus.emit("OnBroadcastStateChanged", {
      txId: hex64("ff"),
      state,
      updatedAtMs: Date.now(),
    });
  }
}

function hex64(seed: string): string {
  return seed.repeat(32).slice(0, 64);
}
