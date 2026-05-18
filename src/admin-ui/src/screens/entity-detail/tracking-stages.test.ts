import { describe, expect, it } from "vitest";
import { readinessStages } from "./tracking-stages";
import type {
  TrackedEntityReadinessResponse,
  TrackedHistoryStatusResponse,
} from "@/types/admin";

function readyHistory(): TrackedHistoryStatusResponse {
  return {
    historyReadiness: "Ready",
    coverage: {
      mode: "Full",
      fullCoverage: true,
      authoritativeFromBlockHeight: 850_000,
      authoritativeFromObservedAt: 1_700_000_000_000,
    },
    backfillStatus: {
      status: "Completed",
      requestedAt: 1_700_000_000_000,
      startedAt: 1_700_000_000_000,
      lastProgressAt: 1_700_000_000_000,
      completedAt: 1_700_000_000_000,
      itemsScanned: 100,
      itemsApplied: 100,
      errorCode: null,
    },
    rootedToken: null,
  };
}

function readiness(overrides: Partial<TrackedEntityReadinessResponse> = {}): TrackedEntityReadinessResponse {
  return {
    tracked: true,
    entityType: "Address",
    entityId: "test",
    lifecycleStatus: "Ready",
    readable: true,
    authoritative: true,
    degraded: false,
    lagBlocks: 0,
    progress: 1,
    history: readyHistory(),
    ...overrides,
  };
}

describe("readinessStages", () => {
  it("returns all-pending stages when readiness is null", () => {
    const stages = readinessStages(null);
    expect(stages).toHaveLength(5);
    expect(stages.every((s) => s.status === "pending")).toBe(true);
  });

  it("marks not-tracked entities as failed at the first stage", () => {
    const stages = readinessStages(readiness({ tracked: false }));
    expect(stages[0].status).toBe("failed");
    expect(stages.slice(1).every((s) => s.status === "pending")).toBe(true);
  });

  it("marks all stages done for a healthy entity (Ready + Completed backfill)", () => {
    const stages = readinessStages(readiness());
    expect(stages.every((s) => s.status === "done")).toBe(true);
  });

  it("marks the authoritative stage failed when degraded", () => {
    const stages = readinessStages(readiness({ degraded: true }));
    expect(stages[3].status).toBe("failed");
  });

  it("flags history as active when backfill is running", () => {
    const stages = readinessStages(
      readiness({
        history: {
          historyReadiness: "Catchup",
          coverage: null,
          backfillStatus: {
            status: "Running",
            requestedAt: 1_700_000_000_000,
            startedAt: 1_700_000_000_000,
            lastProgressAt: 1_700_000_500_000,
            completedAt: null,
            itemsScanned: 1_000,
            itemsApplied: 720,
            errorCode: null,
          },
          rootedToken: null,
        },
      })
    );
    expect(stages[2].status).toBe("active");
    expect(stages[2].detail).toMatch(/Running/);
    expect(stages[2].detail).toMatch(/720\/1000 applied/);
  });

  it("flags history as warning when readiness is Behind and no backfill is in flight", () => {
    const stages = readinessStages(
      readiness({
        history: {
          historyReadiness: "Behind",
          coverage: null,
          backfillStatus: {
            status: "Idle",
            requestedAt: null,
            startedAt: null,
            lastProgressAt: null,
            completedAt: null,
            itemsScanned: 0,
            itemsApplied: 0,
            errorCode: null,
          },
          rootedToken: null,
        },
      })
    );
    expect(stages[2].status).toBe("warning");
  });
});
