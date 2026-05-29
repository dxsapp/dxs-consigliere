import type { IAdminClient } from "@/lib/admin/admin-client";
import { makeAppError } from "@/types/errors";
import type {
  AdminAuditLogResponse,
  AdminPeersResponse,
  AdminProvidersResponse,
  AdminTrackAddressRequest,
  AdminTrackTokenRequest,
  AdminTrackedAddressResponse,
  AdminTrackedTokenResponse,
  BroadcastReceiptDto,
  HeadersTipDto,
  P2pAlertResponse,
  P2pHealthDto,
  SetupCompleteRequest,
  SetupOptionsResponse,
  SetupStatusResponse,
  SourceMetricsResponse,
  SourceMetricsSnapshot,
} from "@/types/admin";
import { SOURCE_KEYS } from "@/types/admin";

/**
 * S4 mock IAdminClient. Mirrors the real backend response shapes
 * exactly (Core Rule §12). Returns a realistic seed:
 *  - 8 active peers in the pool
 *  - 24-snapshot history at ~30s cadence with growing FirstSeen
 *    counters per source
 *  - p2p source gets the highest rate by design
 */
export class MockAdminClient implements IAdminClient {
  /** wave-A3 S3-audit L1: audit-log entries kept as instance state
   *  so `broadcastRaw` can prepend a freshly-stamped record. That
   *  makes the mock-mode smoke ("trigger a broadcast → see the
   *  entry on /audit-log") actually exercise the wired flow. */
  private auditEntries: AdminAuditLogResponse["entries"] = [];
  private auditSeeded = false;

  /** In-memory tracked-entity lists so the track-a-new flow round-trips
   *  in mock mode (POST appends → subsequent GET returns it). Lazily
   *  seeded from the detail seeds on first access. */
  private trackedAddresses: AdminTrackedAddressResponse[] | null = null;
  private trackedTokens: AdminTrackedTokenResponse[] | null = null;

  /** Snapshot timestamps base; tests can override via constructor. */
  constructor(private readonly nowMs: () => number = () => Date.now()) {}

  async getP2pHealth(): Promise<P2pHealthDto> {
    return {
      bound: true,
      poolSize: 8,
      targetPoolSize: 8,
      subnet24Diversity: 6,
      activePeers: [
        "65.108.41.10:8333",
        "178.18.249.131:8333",
        "23.88.74.45:8333",
        "5.9.130.227:8333",
        "144.76.166.214:8333",
        "162.55.91.10:8333",
        "78.46.249.220:8333",
        "95.216.150.10:8333",
      ],
      inboundEnabled: false,
    };
  }

  async getTrackedAddress(address: string): Promise<AdminTrackedAddressResponse> {
    const { seedAddress } = await import("@/lib/mock/admin-tracked-seed");
    return seedAddress(address, this.nowMs());
  }

  async getTrackedToken(tokenId: string): Promise<AdminTrackedTokenResponse> {
    const { seedToken } = await import("@/lib/mock/admin-tracked-seed");
    return seedToken(tokenId, this.nowMs());
  }

  private async ensureTrackedSeeded(): Promise<void> {
    if (this.trackedAddresses && this.trackedTokens) return;
    const { seedAddress, seedToken } = await import("@/lib/mock/admin-tracked-seed");
    const now = this.nowMs();
    if (!this.trackedAddresses) {
      this.trackedAddresses = [
        seedAddress("1HotWallet0000000000000000000000000", now),
        seedAddress("1ColdVault0000000000000000000000000", now),
      ];
    }
    if (!this.trackedTokens) {
      this.trackedTokens = [seedToken("tok-dstas-0001", now)];
    }
  }

  async getTrackedAddresses(includeTombstoned = false): Promise<AdminTrackedAddressResponse[]> {
    await this.ensureTrackedSeeded();
    const all = this.trackedAddresses ?? [];
    return includeTombstoned ? [...all] : all.filter((a) => !a.isTombstoned);
  }

  async getTrackedTokens(includeTombstoned = false): Promise<AdminTrackedTokenResponse[]> {
    await this.ensureTrackedSeeded();
    const all = this.trackedTokens ?? [];
    return includeTombstoned ? [...all] : all.filter((t) => !t.isTombstoned);
  }

  async trackAddress(req: AdminTrackAddressRequest): Promise<AdminTrackedAddressResponse> {
    await this.ensureTrackedSeeded();
    const address = (req.address ?? "").trim();
    if (!address) throw makeAppError("Validation", "address_required", 400);
    if (this.trackedAddresses!.some((a) => a.address === address)) {
      throw makeAppError("Validation", "already_tracked", 400);
    }
    const { seedAddress } = await import("@/lib/mock/admin-tracked-seed");
    const created: AdminTrackedAddressResponse = {
      ...seedAddress(address, this.nowMs()),
      name: (req.name ?? "").trim() || address,
    };
    this.trackedAddresses = [created, ...this.trackedAddresses!];
    return created;
  }

  async trackToken(req: AdminTrackTokenRequest): Promise<AdminTrackedTokenResponse> {
    await this.ensureTrackedSeeded();
    const tokenId = (req.tokenId ?? "").trim();
    if (!tokenId) throw makeAppError("Validation", "token_required", 400);
    if (this.trackedTokens!.some((t) => t.tokenId === tokenId)) {
      throw makeAppError("Validation", "already_tracked", 400);
    }
    const { seedToken } = await import("@/lib/mock/admin-tracked-seed");
    const created: AdminTrackedTokenResponse = {
      ...seedToken(tokenId, this.nowMs()),
      symbol: (req.symbol ?? "").trim() || "TOKEN",
    };
    this.trackedTokens = [created, ...this.trackedTokens!];
    return created;
  }

  async getAlerts(opts: { lastN?: number; since?: number } = {}): Promise<P2pAlertResponse> {
    const { seedAlerts } = await import("@/lib/mock/admin-systems-seed");
    return seedAlerts(this.nowMs(), opts);
  }

  async getPeers(): Promise<AdminPeersResponse> {
    const { seedPeers } = await import("@/lib/mock/admin-systems-seed");
    return seedPeers(this.nowMs());
  }

  async getHeadersTip(): Promise<HeadersTipDto | null> {
    const { seedHeadersTip } = await import("@/lib/mock/admin-systems-seed");
    return seedHeadersTip(this.nowMs());
  }

  async getHeadersRecent(count: number): Promise<HeadersTipDto[]> {
    const { seedHeadersRecent } = await import("@/lib/mock/admin-systems-seed");
    return seedHeadersRecent(this.nowMs(), count);
  }

  async getProviders(): Promise<AdminProvidersResponse> {
    const { seedProviders } = await import("@/lib/mock/admin-systems-seed");
    return seedProviders();
  }

  async getSetupStatus(): Promise<SetupStatusResponse> {
    return readMockSetupStatus();
  }

  async getSetupOptions(): Promise<SetupOptionsResponse> {
    const { seedSetupOptions } = await import("@/lib/mock/admin-systems-seed");
    return seedSetupOptions(readMockSetupStatus());
  }

  async completeSetup(req: SetupCompleteRequest): Promise<SetupStatusResponse> {
    // The real backend rejects an already-completed install with
    // 409. Mirror that so the e2e + page tests can pin the flow.
    const current = readMockSetupStatus();
    if (current.setupCompleted) {
      throw makeAppError("Validation", "setup_already_completed", 409);
    }
    const username = req.admin.username.trim();
    const next: SetupStatusResponse = {
      setupRequired: false,
      setupCompleted: true,
      adminEnabled: req.admin.enabled,
      adminUsername: username || null,
    };
    writeMockSetupStatus(next);
    // Pre-seed the persistent MockAuthClient state so signing in
    // with the operator's chosen credentials Just Works after
    // redirect. Mirrors the real cookie-mode behaviour where the
    // setup wizard creates the admin account.
    if (req.admin.enabled && username && req.admin.password) {
      writeMockAuthCredentials({ username, password: req.admin.password });
    }
    return next;
  }

  async getAuditLog(opts: {
    since?: number;
    action?: string;
    username?: string;
    lastN?: number;
  } = {}): Promise<AdminAuditLogResponse> {
    this.seedAuditEntriesIfNeeded();
    const filtered = this.auditEntries.filter((entry) => {
      if (opts.since && entry.unixMs < opts.since) return false;
      if (opts.action && entry.action !== opts.action) return false;
      if (opts.username && entry.username !== opts.username) return false;
      return true;
    });
    const take = opts.lastN && opts.lastN > 0 ? Math.min(opts.lastN, filtered.length) : filtered.length;
    return { totalMatched: filtered.length, entries: filtered.slice(0, take) };
  }

  async broadcastRaw(rawHex: string, _signal?: AbortSignal): Promise<BroadcastReceiptDto> {
    // Deterministic pseudo-txid: sha-like fold of rawHex; we only
    // need a stable 64-hex-char string for the UI confirmation.
    const txId = hexFold(rawHex);
    const unixMs = this.nowMs();

    // wave-A3 S3-audit L1: the real backend fail-stops the
    // broadcast on `IAuditLogger.RecordAsync == false`; the
    // mock represents the happy path by recording the matching
    // forensic entry BEFORE returning the receipt so a user
    // flipping to `/audit-log` immediately after the broadcast
    // sees the new row. Keeping the audit list newest-first
    // mirrors the backend `OrderByDescending(UnixMs)` shape.
    this.seedAuditEntriesIfNeeded();
    this.auditEntries = [
      {
        id: `audit-log/${unixMs}/${randomMockId()}`,
        unixMs,
        username: "admin",
        action: "broadcast_tx",
        targetId: txId,
        context: JSON.stringify({ rawHexLength: rawHex.length, source: "operator" }),
      },
      ...this.auditEntries,
    ];

    return {
      txId,
      state: "Validated",
      createdAtMs: unixMs,
      failReason: null,
    };
  }

  private seedAuditEntriesIfNeeded(): void {
    if (this.auditSeeded) return;
    const now = this.nowMs();
    this.auditEntries = [
      {
        id: `audit-log/${now - 5_000}/aabbccdd`,
        unixMs: now - 5_000,
        username: "admin",
        action: "broadcast_tx",
        targetId: "f".repeat(64),
        context: JSON.stringify({ rawHexLength: 512, source: "operator" }),
      },
      {
        id: `audit-log/${now - 600_000}/00112233`,
        unixMs: now - 600_000,
        username: "admin",
        action: "broadcast_tx",
        targetId: "e".repeat(64),
        context: JSON.stringify({ rawHexLength: 384, source: "operator" }),
      },
      {
        id: `audit-log/${now - 3_600_000}/44556677`,
        unixMs: now - 3_600_000,
        username: "ops",
        action: "broadcast_tx",
        targetId: "d".repeat(64),
        context: JSON.stringify({ rawHexLength: 256, source: "operator" }),
      },
    ];
    this.auditSeeded = true;
  }

  async getSourceMetrics(opts: { lastN?: number } = {}): Promise<SourceMetricsResponse> {
    const lastN = Math.max(0, opts.lastN ?? 0);
    const history = lastN > 0 ? this.synthHistory(lastN) : [];
    const latest = this.snapshotAt(this.nowMs(), 0);
    return { latest, history };
  }

  private synthHistory(count: number): SourceMetricsSnapshot[] {
    const intervalMs = 30_000;
    const out: SourceMetricsSnapshot[] = [];
    const now = this.nowMs();
    // History is oldest-first per the C# response convention.
    for (let i = count - 1; i >= 0; i--) {
      out.push(this.snapshotAt(now - i * intervalMs, count - 1 - i));
    }
    return out;
  }

  private snapshotAt(snapshotUnixMs: number, ageIndex: number): SourceMetricsSnapshot {
    // Each source gets a stable growth curve; ageIndex 0 = newest.
    const ratePerSource = { p2p: 12, bitails: 11, junglebus: 4 } as const;
    const visibilityCounters = Object.fromEntries(
      SOURCE_KEYS.map((src) => [
        src,
        {
          firstSeen: 1_000 + ratePerSource[src] * ageIndex,
          onlySaw: Math.floor(ratePerSource[src] * 0.1 * ageIndex),
          lagBuckets: [50, 80, 60, 30, 12, 4],
        },
      ])
    );
    const observationCounters = Object.fromEntries(
      SOURCE_KEYS.map((src) => [
        src,
        {
          invObserved: 2_000 + ratePerSource[src] * ageIndex,
          matched: 1_900 + ratePerSource[src] * ageIndex,
          unmatched: 100 + Math.floor(ratePerSource[src] * 0.05 * ageIndex),
          parseError: 0,
          rateLimited: 0,
          getDataTimeout: 0,
          oversizePayload: 0,
        },
      ])
    );
    return {
      id: `metrics/sources/${snapshotUnixMs.toString().padStart(14, "0")}`,
      snapshotUnixMs,
      observationCounters,
      visibilityCounters,
      rebroadcast: {
        announced: 24,
        skippedNoRaw: 0,
        skippedCoinbase: 1,
        announceNoReadyPeer: 0,
        announceFailed: 0,
      },
      lastDegradedReorgAt: null,
    };
  }
}

// ── wave-A2 S0: persistent setup state for mock mode ──────────────
//
// MockAuthClient already persists `{authenticated, username}` in
// localStorage so e2e specs survive full SPA reloads (see
// `src/lib/mock/auth.ts`). The wizard adds a second slice: a
// `setupCompleted` flag + admin-credentials seed so a fresh e2e
// run can:
//   1. Visit `/` → bounced to `/setup` (setupCompleted=false)
//   2. Submit the wizard → mock flips the flag + writes the chosen
//      credentials into the auth state
//   3. Redirect to `/login` → sign in with those credentials
//
// Both keys are cleared by `src/test-setup.ts`'s afterEach.

const SETUP_STATE_KEY = "consigliere-admin/mock-setup-state/v1";
const MOCK_AUTH_CREDENTIALS_KEY = "consigliere-admin/mock-auth-credentials/v1";

function readMockSetupStatus(): SetupStatusResponse {
  if (typeof window === "undefined") {
    return { setupRequired: true, setupCompleted: false, adminEnabled: false, adminUsername: null };
  }
  try {
    const raw = window.localStorage.getItem(SETUP_STATE_KEY);
    if (!raw) {
      return { setupRequired: true, setupCompleted: false, adminEnabled: false, adminUsername: null };
    }
    const parsed = JSON.parse(raw);
    return {
      setupRequired: parsed.setupRequired !== false,
      setupCompleted: parsed.setupCompleted === true,
      adminEnabled: parsed.adminEnabled === true,
      adminUsername: typeof parsed.adminUsername === "string" ? parsed.adminUsername : null,
    };
  } catch {
    return { setupRequired: true, setupCompleted: false, adminEnabled: false, adminUsername: null };
  }
}

function writeMockSetupStatus(s: SetupStatusResponse): void {
  if (typeof window === "undefined") return;
  try {
    window.localStorage.setItem(SETUP_STATE_KEY, JSON.stringify(s));
  } catch {
    /* swallow */
  }
}

function writeMockAuthCredentials(creds: { username: string; password: string }): void {
  if (typeof window === "undefined") return;
  try {
    window.localStorage.setItem(MOCK_AUTH_CREDENTIALS_KEY, JSON.stringify(creds));
  } catch {
    /* swallow */
  }
}

/** Deterministic 64-hex-char fold of an arbitrary string. Suitable
 *  for mocked txIds where we just want a stable identifier per
 *  input. Not a real hash — never use for production semantics. */
/** 8-char id suffix for mock audit entries. Deterministic-ish
 *  per call; uniqueness is the only requirement. */
function randomMockId(): string {
  return Math.random().toString(16).slice(2, 10).padStart(8, "0");
}

function hexFold(input: string): string {
  let h1 = 0x811c9dc5;
  let h2 = 0xdeadbeef;
  for (let i = 0; i < input.length; i++) {
    const c = input.charCodeAt(i);
    h1 = Math.imul(h1 ^ c, 16777619) >>> 0;
    h2 = Math.imul(h2 ^ c, 2246822519) >>> 0;
  }
  const seed = h1.toString(16).padStart(8, "0") + h2.toString(16).padStart(8, "0");
  return seed.repeat(4).slice(0, 64);
}
