import type { EntityTimelineStage } from "@/screens/entity-detail/EntityTimeline";
import type { TrackedEntityReadinessResponse } from "@/types/admin";

/**
 * Maps an admin `TrackedEntityReadinessResponse` onto the shared
 * vertical Stepper used by Address + Token detail screens.
 *
 * The 5 stages mirror the backend tracking pipeline:
 *
 *   Tracked → Discovered → History caught up → Authoritative → Ready
 *
 * Failure surfaces as a "failed" status on whichever stage owns the
 * problem (degraded → Authoritative; not-tracked → Tracked).
 */
export function readinessStages(r: TrackedEntityReadinessResponse | null): EntityTimelineStage[] {
  if (!r) {
    return BASE.map((b) => ({ ...b, status: "pending" }));
  }

  const history = r.history;
  const historyCaught = history?.status === "UpToDate";
  const historyPending = (history?.pendingCount ?? 0) > 0;

  return [
    {
      key: "tracked",
      label: "Tracked",
      status: r.tracked ? "done" : "failed",
      detail: r.tracked ? null : "Not currently tracked",
    },
    {
      key: "readable",
      label: "Discovered",
      status: !r.tracked ? "pending" : r.readable ? "done" : "active",
      detail: r.readable ? null : "Discovering UTXOs / first observation",
    },
    {
      key: "history",
      label: "History caught up",
      status: !r.tracked
        ? "pending"
        : !r.readable
        ? "pending"
        : historyCaught
        ? "done"
        : historyPending
        ? "active"
        : "warning",
      timestampMs: history?.lastCheckpoint ?? null,
      detail: history
        ? `${history.status}${
            historyPending ? ` · ${history.pendingCount} pending` : ""
          }`
        : "No history snapshot yet",
    },
    {
      key: "authoritative",
      label: "Authoritative",
      status: !r.tracked
        ? "pending"
        : r.degraded
        ? "failed"
        : r.authoritative
        ? "done"
        : "active",
      detail: r.degraded
        ? "Degraded — backing source has a known incident"
        : r.lagBlocks !== null && r.lagBlocks > 0
        ? `Lag ${r.lagBlocks} block(s)`
        : null,
    },
    {
      key: "ready",
      label: "Ready",
      status: !r.tracked
        ? "pending"
        : r.lifecycleStatus === "Ready"
        ? "done"
        : "active",
      detail: r.lifecycleStatus,
    },
  ];
}

const BASE: Omit<EntityTimelineStage, "status">[] = [
  { key: "tracked", label: "Tracked" },
  { key: "readable", label: "Discovered" },
  { key: "history", label: "History caught up" },
  { key: "authoritative", label: "Authoritative" },
  { key: "ready", label: "Ready" },
];
