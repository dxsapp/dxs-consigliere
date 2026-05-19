import type { TestProject } from "vitest/node";
import { startHost, type HostHandle } from "./_host-harness";

/**
 * wave-A2 S2-audit H1: vitest globalSetup runs in the MAIN
 * process — the right place to spawn + tear down the backend.
 * `provide()` ships the base URL to forks via `inject()`; the
 * teardown sends SIGTERM/SIGKILL escalation so the dotnet child
 * is reliably reaped (previously `process.on("beforeExit")` did
 * NOT fire because the live child kept the event loop active).
 */

declare module "vitest" {
  export interface ProvidedContext {
    contractHostBaseUrl: string;
  }
}

let host: HostHandle | null = null;

export async function setup(project: TestProject) {
  host = await startHost();
  project.provide("contractHostBaseUrl", host.baseUrl);
}

export async function teardown() {
  if (host) {
    await host.stop();
    host = null;
  }
}
