import { spawn, spawnSync, type ChildProcess } from "node:child_process";
import { existsSync, mkdtempSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { join, resolve } from "node:path";

/**
 * wave-A2 S2 — child-process harness for the ASP.NET-host
 * parity tests.
 *
 * Spawns the Consigliere host with `ASPNETCORE_ENVIRONMENT=Test`
 * + `ASPNETCORE_URLS=http://127.0.0.1:0` so Kestrel picks an
 * arbitrary free port, parses the assigned port from stdout, and
 * lets every spec hit `baseUrl`.
 *
 * `RavenDb__Urls__0` overrides the appsettings.Test.json value;
 * CI sets it to the service-container URL, local dev defaults to
 * `http://localhost:8080` (matches compose.yml).
 *
 * Cookies persist across requests inside a single `fetchAuthed`
 * jar so admin-protected endpoints can be exercised after a
 * single login.
 */

export interface HostHandle {
  baseUrl: string;
  cookieJar: Map<string, string>;
  stop(): Promise<void>;
}

interface SpawnOptions {
  ravenUrl?: string;
  port?: number;
  configuration?: "Debug" | "Release";
}

const HERE = resolve(import.meta.dirname);
const ADMIN_UI_ROOT = resolve(HERE, "..", "..");
const BACKEND_PROJECT = resolve(ADMIN_UI_ROOT, "..", "Dxs.Consigliere", "Dxs.Consigliere.csproj");

const KESTREL_LISTENING_RX = /Now listening on:\s+(https?:\/\/[^\s]+)/;

export async function startHost(opts: SpawnOptions = {}): Promise<HostHandle> {
  const ravenUrl = opts.ravenUrl ?? process.env.RAVEN_URL ?? "http://localhost:8080";
  const port = opts.port ?? 0;
  const configuration = opts.configuration ?? (process.env.CI ? "Release" : "Debug");

  const dataLogDir = mkdtempSync(join(tmpdir(), "consigliere-contract-"));
  const env: NodeJS.ProcessEnv = {
    ...process.env,
    ASPNETCORE_ENVIRONMENT: "Test",
    DOTNET_ENVIRONMENT: "Test",
    ASPNETCORE_URLS: `http://127.0.0.1:${port}`,
    RavenDb__Urls__0: ravenUrl,
    // Keep Microsoft.Hosting.Lifetime at Info — we parse the
    // "Now listening on" line below.
    Logging__LogLevel__Microsoft__Hosting__Lifetime: "Information",
  };

  // Build first (no-op if up-to-date) then spawn the assembly
  // directly. `dotnet run` inserts a `dotnet exec` wrapper that
  // doesn't propagate SIGTERM, so we end up with an orphan child
  // process and vitest hangs on globalSetup teardown.
  const buildResult = spawnSync(
    "dotnet",
    ["build", BACKEND_PROJECT, "-c", configuration, "-p:SkipAdminUiBuild=true", "-v:quiet", "--nologo"],
    { stdio: "inherit" }
  );
  if (buildResult.status !== 0) {
    throw new Error(`dotnet build failed with exit ${buildResult.status}`);
  }
  const assemblyPath = resolve(
    BACKEND_PROJECT,
    "..",
    "bin",
    configuration,
    "net9.0",
    "Dxs.Consigliere.dll"
  );
  if (!existsSync(assemblyPath)) {
    throw new Error(`Built assembly not found at ${assemblyPath}`);
  }

  const child = spawn("dotnet", [assemblyPath], {
    env,
    cwd: resolve(BACKEND_PROJECT, ".."),
    stdio: ["ignore", "pipe", "pipe"],
  });

  child.stdout?.setEncoding("utf-8");
  child.stderr?.setEncoding("utf-8");

  const baseUrl = await waitForListening(child);
  // Drain remaining stdout/stderr to prevent backpressure.
  child.stdout?.on("data", () => {
    /* swallow */
  });
  child.stderr?.on("data", () => {
    /* swallow */
  });

  return {
    baseUrl,
    cookieJar: new Map<string, string>(),
    async stop() {
      await stopChild(child);
      rmSync(dataLogDir, { recursive: true, force: true });
    },
  };
}

function waitForListening(child: ChildProcess): Promise<string> {
  return new Promise((resolvePromise, rejectPromise) => {
    let stdoutBuffer = "";
    let stderrBuffer = "";
    const timer = setTimeout(() => {
      cleanup();
      rejectPromise(
        new Error(
          `Backend host did not log "Now listening on:" within 120s.\nstdout tail: ${stdoutBuffer.slice(-1500)}\nstderr tail: ${stderrBuffer.slice(-1500)}`
        )
      );
    }, 120_000);

    const onStdout = (chunk: string) => {
      stdoutBuffer += chunk;
      const match = KESTREL_LISTENING_RX.exec(stdoutBuffer);
      if (match) {
        cleanup();
        resolvePromise(match[1].replace(/\/$/, ""));
      }
    };
    const onStderr = (chunk: string) => {
      stderrBuffer += chunk;
      const match = KESTREL_LISTENING_RX.exec(stderrBuffer);
      if (match) {
        cleanup();
        resolvePromise(match[1].replace(/\/$/, ""));
      }
    };
    const onExit = (code: number | null) => {
      cleanup();
      rejectPromise(
        new Error(
          `Backend host exited with code ${code} before listening.\nstdout tail: ${stdoutBuffer.slice(-1500)}\nstderr tail: ${stderrBuffer.slice(-1500)}`
        )
      );
    };

    function cleanup() {
      clearTimeout(timer);
      child.stdout?.off("data", onStdout);
      child.stderr?.off("data", onStderr);
      child.off("exit", onExit);
    }

    child.stdout?.on("data", onStdout);
    child.stderr?.on("data", onStderr);
    child.on("exit", onExit);
  });
}

async function stopChild(child: ChildProcess): Promise<void> {
  if (child.exitCode !== null || child.signalCode !== null) return;
  child.kill("SIGTERM");
  await new Promise<void>((resolveExit) => {
    const timer = setTimeout(() => {
      child.kill("SIGKILL");
      resolveExit();
    }, 10_000);
    child.once("exit", () => {
      clearTimeout(timer);
      resolveExit();
    });
  });
}

/** Cookie-jar-aware fetch wrapper for the contract suite. */
export async function callApi(
  host: HostHandle,
  path: string,
  init: RequestInit = {}
): Promise<Response> {
  const headers = new Headers(init.headers);
  if (host.cookieJar.size > 0) {
    headers.set(
      "Cookie",
      Array.from(host.cookieJar.entries())
        .map(([k, v]) => `${k}=${v}`)
        .join("; ")
    );
  }
  const url = `${host.baseUrl}${path}`;
  const response = await fetch(url, { ...init, headers });
  const setCookie = response.headers.getSetCookie?.() ?? [];
  for (const sc of setCookie) {
    const [pair] = sc.split(";");
    const eq = pair.indexOf("=");
    if (eq <= 0) continue;
    host.cookieJar.set(pair.slice(0, eq).trim(), pair.slice(eq + 1).trim());
  }
  return response;
}
