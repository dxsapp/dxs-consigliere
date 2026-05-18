import type {
  AdminPeersResponse,
  AdminProvidersResponse,
  HeadersTipDto,
  P2pAlertEventDto,
  P2pAlertResponse,
} from "@/types/admin";

/**
 * S7-S10 — dynamic-imported seeds for the system screens.
 * Mirrors the S5 pattern so the shell stays under the A1 M3 200 KB
 * ceiling.
 */

export function seedAlerts(
  now: number,
  opts: { lastN?: number; since?: number } = {}
): P2pAlertResponse {
  const sample: P2pAlertEventDto[] = [
    {
      id: "alert-001",
      alertUnixMs: now - 90_000,
      type: "SourceFirstDropout",
      detail: "Bitails first-seen rate < 0.5/min over last 5 min",
      context: { source: "bitails", windowMinutes: "5", rate: "0.21" },
    },
    {
      id: "alert-002",
      alertUnixMs: now - 12 * 60_000,
      type: "PoolSizeBelowThreshold",
      detail: "P2P pool dropped to 5/8",
      context: { current: "5", target: "8" },
    },
    {
      id: "alert-003",
      alertUnixMs: now - 4 * 60 * 60_000,
      type: "ReorgDepthExceeded",
      detail: "Reorg depth 3 exceeded warn threshold 2",
      context: { depth: "3", threshold: "2" },
    },
    {
      id: "alert-004",
      alertUnixMs: now - 26 * 60 * 60_000,
      type: "RelayBackRateBelowThreshold",
      detail: "Relay-back rate 0.42 below threshold 0.6",
      context: { rate: "0.42", threshold: "0.6", windowMin: "30" },
    },
  ];
  const filtered = opts.since
    ? sample.filter((a) => a.alertUnixMs > (opts.since ?? 0))
    : sample;
  const capped =
    opts.lastN && opts.lastN > 0 ? filtered.slice(0, opts.lastN) : filtered;
  return { alerts: capped };
}

export function seedPeers(now: number): AdminPeersResponse {
  const seeds = [
    "65.108.41.10:8333",
    "178.18.249.131:8333",
    "23.88.74.45:8333",
    "5.9.130.227:8333",
    "144.76.166.214:8333",
    "162.55.91.10:8333",
    "78.46.249.220:8333",
    "95.216.150.10:8333",
    "94.130.184.21:8333",
    "168.119.71.10:8333",
  ];
  const peers = seeds.map((endpoint, i) => {
    const successCount = 8 + (i % 5);
    const failCount = i % 3;
    return {
      endpoint,
      source: i % 3 === 0 ? "Hardcoded" : "DnsSeed",
      userAgent: "/Bitcoin SV:1.0.16/",
      protocolVersion: 70016,
      services: 0x21,
      successCount,
      failCount,
      firstSeen: new Date(now - (10 + i) * 24 * 60 * 60 * 1000).toISOString(),
      lastSeen: new Date(now - (i % 4) * 60_000).toISOString(),
      lastConnected: i < 8 ? new Date(now - (i % 4) * 60_000).toISOString() : null,
      negativeUntil: i >= 8 ? new Date(now + 10 * 60_000).toISOString() : null,
      lastFailureReason: i >= 8 ? "connect timeout" : null,
      subnet24: endpoint.split(".").slice(0, 3).join(".") + ".0/24",
    };
  });
  const distinctSubnets = new Set(peers.map((p) => p.subnet24)).size;
  return {
    total: peers.length,
    successful: peers.filter((p) => p.successCount > 0).length,
    failed: peers.filter((p) => p.failCount > 0).length,
    distinctSubnets,
    peers,
  };
}

export function seedHeadersTip(now: number): HeadersTipDto {
  return {
    hash: "00000000000000000abc" + "00".repeat(22),
    height: 902_815,
    timestampMs: now - 8 * 60_000,
    prevHash: "00000000000000000abe" + "00".repeat(22),
  };
}

export function seedHeadersRecent(now: number, count: number): HeadersTipDto[] {
  const n = Math.max(0, Math.min(count, 100));
  const out: HeadersTipDto[] = [];
  for (let i = 0; i < n; i++) {
    out.push({
      hash: hexPad(`00000000000000000${(0x100 - i).toString(16)}`, 64),
      height: 902_815 - i,
      timestampMs: now - (8 + i * 10) * 60_000,
      prevHash: hexPad(`00000000000000000${(0x101 - i).toString(16)}`, 64),
    });
  }
  return out;
}

export function seedProviders(): AdminProvidersResponse {
  const baseValues = {
    realtimePrimaryProvider: "Bitails",
    rawTxPrimaryProvider: "Bitails",
    restPrimaryProvider: "Whatsonchain",
    bitailsTransport: "Websocket",
    bitails: {
      apiKey: "***",
      baseUrl: "https://api.bitails.io",
      websocketBaseUrl: "wss://ws.bitails.io",
      zmqTxUrl: null,
      zmqBlockUrl: null,
    },
    whatsonchain: {
      apiKey: "",
      baseUrl: "https://api.whatsonchain.com/v1/bsv/main",
    },
    junglebus: {
      baseUrl: "https://junglebus.gorillapool.io",
      mempoolSubscriptionId: "sub-mem-001",
      blockSubscriptionId: "sub-blk-001",
    },
  };
  return {
    recommendations: {
      realtimePrimaryProvider: "Bitails",
      restPrimaryProvider: "Whatsonchain",
      rawTxFetchProvider: "Bitails",
    },
    config: {
      static: baseValues,
      override: null,
      effective: baseValues,
      overrideActive: false,
      restartRequired: false,
      allowedRealtimePrimaryProviders: ["Bitails", "JungleBus", "P2P"],
      allowedRawTxPrimaryProviders: ["Bitails", "Whatsonchain"],
      allowedRestPrimaryProviders: ["Whatsonchain", "Bitails"],
      allowedBitailsTransports: ["Websocket", "Zmq"],
      updatedAt: null,
      updatedBy: null,
    },
    providers: [
      {
        providerId: "Bitails",
        displayName: "Bitails",
        roles: ["realtime", "rawTx"],
        supportedCapabilities: ["Websocket", "Zmq", "RawTx"],
        recommendedFor: ["realtime", "rawTx"],
        activeFor: ["realtime", "rawTx"],
        status: "Ready",
        description: "Bitails Websocket + Zmq realtime feed.",
        missingRequirements: [],
        helpLinks: [{ label: "Docs", url: "https://docs.bitails.io" }],
      },
      {
        providerId: "Whatsonchain",
        displayName: "WhatsOnChain",
        roles: ["rest"],
        supportedCapabilities: ["Rest"],
        recommendedFor: ["rest"],
        activeFor: ["rest"],
        status: "Ready",
        description: "WhatsOnChain REST API.",
        missingRequirements: [],
        helpLinks: [{ label: "Docs", url: "https://developers.whatsonchain.com/" }],
      },
      {
        providerId: "JungleBus",
        displayName: "JungleBus",
        roles: ["realtime"],
        supportedCapabilities: ["Websocket"],
        recommendedFor: [],
        activeFor: [],
        status: "Disabled",
        description: "GorillaPool JungleBus subscription feed.",
        missingRequirements: ["mempoolSubscriptionId"],
        helpLinks: [{ label: "Docs", url: "https://junglebus.gorillapool.io" }],
      },
    ],
  };
}

function hexPad(s: string, width: number): string {
  const padded = s.replace(/^0x/, "");
  return padded.length >= width ? padded.slice(0, width) : "0".repeat(width - padded.length) + padded;
}
