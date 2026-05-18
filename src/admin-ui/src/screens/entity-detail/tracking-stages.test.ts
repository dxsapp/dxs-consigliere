import { describe, expect, it } from "vitest";
import { readinessStages } from "./tracking-stages";
import type { TrackedEntityReadinessResponse } from "@/types/admin";

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
    history: { status: "UpToDate", pendingCount: 0 },
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

  it("marks all stages done for a healthy entity", () => {
    const stages = readinessStages(readiness());
    expect(stages.every((s) => s.status === "done")).toBe(true);
  });

  it("marks the authoritative stage failed when degraded", () => {
    const stages = readinessStages(readiness({ degraded: true }));
    expect(stages[3].status).toBe("failed");
  });

  it("flags history as active when pending events remain", () => {
    const stages = readinessStages(
      readiness({
        history: { status: "Catchup", pendingCount: 7 },
      })
    );
    expect(stages[2].status).toBe("active");
    expect(stages[2].detail).toMatch(/7 pending/);
  });
});
