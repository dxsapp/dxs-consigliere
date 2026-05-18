import { BrowserRouter, Navigate, Route, Routes } from "react-router-dom";
import { lazy, Suspense, useEffect, useState } from "react";
import { PlaceholderPage } from "@/screens/_placeholder/PlaceholderPage";
import { RootStore } from "@/stores/root";
import { hydratePrefStore } from "@/stores/pref.store";
import { ThemeProvider } from "@/app/ThemeProvider";
import { AuthGuard } from "@/app/AuthGuard";
import { AppShell } from "@/components/shell/AppShell";
import { LANDING_PATH, LOGIN_PATH } from "@/app/routes";

// S1-audit L3 + S2-audit bundle-headroom + S3-audit M6: every
// non-shell page is lazy-loaded so the cold-load shell stays
// under A1 M3's 200 KB ceiling.
const DevThemeDemoPage = lazy(() =>
  import("@/screens/dev-theme-demo/DevThemeDemoPage").then((m) => ({
    default: m.DevThemeDemoPage,
  }))
);
const LoginPage = lazy(() =>
  import("@/screens/login/LoginPage").then((m) => ({ default: m.LoginPage }))
);
const DashboardPage = lazy(() =>
  import("@/screens/dashboard/DashboardPage").then((m) => ({ default: m.DashboardPage }))
);
const TransactionDetailPage = lazy(() =>
  import("@/screens/entity-detail/TransactionDetailPage").then((m) => ({
    default: m.TransactionDetailPage,
  }))
);
const AddressDetailPage = lazy(() =>
  import("@/screens/entity-detail/AddressDetailPage").then((m) => ({
    default: m.AddressDetailPage,
  }))
);
const TokenDetailPage = lazy(() =>
  import("@/screens/entity-detail/TokenDetailPage").then((m) => ({
    default: m.TokenDetailPage,
  }))
);
const BroadcastQueuePage = lazy(() =>
  import("@/screens/broadcast-queue/BroadcastQueuePage").then((m) => ({
    default: m.BroadcastQueuePage,
  }))
);
const AlertsPage = lazy(() =>
  import("@/screens/alerts/AlertsPage").then((m) => ({ default: m.AlertsPage }))
);
const P2pPage = lazy(() =>
  import("@/screens/p2p/P2pPage").then((m) => ({ default: m.P2pPage }))
);

// S6-audit M2: RootStore is constructed asynchronously because the
// mock-mode factory dynamic-imports the mock module on demand. In
// real mode the await resolves on the same tick (no UX delay).
let rootInstance: RootStore | null = null;

// Vite-injected env. Override via VITE_CONSIGLIERE_ENV at build time.
const ENV_LABEL = (import.meta.env.VITE_CONSIGLIERE_ENV as string | undefined) ?? "mainnet";

export function App() {
  const [root, setRoot] = useState<RootStore | null>(rootInstance);

  useEffect(() => {
    // S1-audit M1: hydratePrefStore is idempotent (StrictMode double-
    // mount safe) and failure-safe (storage error resets defaults +
    // marks hydrated). No additional catch needed here.
    // S3: auth.hydrate() resolves the cookie session (anonymous if
    // none) before the route guard renders, so the user doesn't
    // bounce to /login during a fresh page load. SignalR is started
    // best-effort after both prefs + auth complete — its failure
    // surfaces as `connection: "offline"` in the shell, not an app
    // crash.
    let cancelled = false;
    void (async () => {
      const r = rootInstance ?? (await RootStore.build());
      rootInstance = r;
      await Promise.all([hydratePrefStore(r.prefs), r.auth.hydrate()]);
      if (cancelled) return;
      // Kick off SignalR; failures bubble through the bus as
      // connection-offline. Do NOT block render on this.
      void r.signalR.start().catch(() => {
        /* connection-offline already emitted by the client */
      });
      setRoot(r);
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  if (!root) return null;

  return (
    <ThemeProvider prefs={root.prefs}>
      <BrowserRouter>
        <Routes>
          <Route
            path={LOGIN_PATH}
            element={
              <Suspense fallback={null}>
                <LoginPage auth={root.auth} />
              </Suspense>
            }
          />

          <Route
            path="/dev/theme-demo"
            element={
              <Suspense fallback={null}>
                <DevThemeDemoPage prefs={root.prefs} />
              </Suspense>
            }
          />

          {/* Every other path is guarded + wrapped in the shell. */}
          <Route
            path="/*"
            element={
              <AuthGuard auth={root.auth}>
                <AppShell auth={root.auth} prefs={root.prefs} shell={root.shell} env={ENV_LABEL}>
                  <AuthedRoutes root={root} />
                </AppShell>
              </AuthGuard>
            }
          />
        </Routes>
      </BrowserRouter>
    </ThemeProvider>
  );
}

/**
 * All 14 authed screens wired to placeholders. Each S4-S10 slice
 * swaps the `element` for the real implementation in-place.
 */
function AuthedRoutes({ root }: { root: RootStore }) {
  return (
    <Routes>
      <Route path="/" element={<Navigate to={LANDING_PATH} replace />} />

      {/* Operator section. */}
      <Route
        path="/dashboard"
        element={
          <Suspense fallback={null}>
            <DashboardPage admin={root.admin} bus={root.bus} />
          </Suspense>
        }
      />
      <Route path="/transactions" element={<PlaceholderPage id="transactions" title="Transactions" ownerSlice="S5+" description="Lookup landing — header search is the primary entrypoint." />} />
      <Route
        path="/transactions/:txid"
        element={
          <Suspense fallback={null}>
            <TransactionDetailPage bus={root.bus} signalR={root.signalR} />
          </Suspense>
        }
      />
      <Route
        path="/broadcast-queue"
        element={
          <Suspense fallback={null}>
            <BroadcastQueuePage admin={root.admin} bus={root.bus} />
          </Suspense>
        }
      />
      <Route path="/addresses" element={<PlaceholderPage id="addresses" title="Addresses" ownerSlice="S5+" description="Address lookup landing." />} />
      <Route
        path="/addresses/:address"
        element={
          <Suspense fallback={null}>
            <AddressDetailPage admin={root.admin} />
          </Suspense>
        }
      />
      <Route path="/tokens" element={<PlaceholderPage id="tokens" title="Tokens" ownerSlice="S5+" description="DSTAS / native token lookup landing." />} />
      <Route
        path="/tokens/:tokenId"
        element={
          <Suspense fallback={null}>
            <TokenDetailPage admin={root.admin} />
          </Suspense>
        }
      />
      <Route
        path="/alerts"
        element={
          <Suspense fallback={null}>
            <AlertsPage admin={root.admin} />
          </Suspense>
        }
      />

      {/* System section. */}
      <Route
        path="/p2p"
        element={
          <Suspense fallback={null}>
            <P2pPage admin={root.admin} />
          </Suspense>
        }
      />
      <Route path="/metrics/sources" element={<PlaceholderPage id="source-metrics" title="Source Metrics" ownerSlice="S9" description="Per-source observation + visibility deltas." />} />
      <Route path="/headers" element={<PlaceholderPage id="headers" title="Headers Chain" ownerSlice="S9" description="Tip + recent headers + reorg log." />} />
      <Route path="/broadcast-inspector" element={<PlaceholderPage id="broadcast-inspector" title="Broadcast Inspector" ownerSlice="S9" description="Submit form + lifecycle watch." />} />
      <Route path="/configuration" element={<PlaceholderPage id="configuration" title="Configuration" ownerSlice="S10" description="Read-only sectioned view of BsvP2pConfig + Alert + Inbound." />} />
      <Route path="/logs" element={<PlaceholderPage id="logs" title="Logs / Raw" ownerSlice="S10" description="Journal browser + raw doc viewer with sanitizer." />} />
      <Route path="/providers" element={<PlaceholderPage id="providers" title="Providers" ownerSlice="S10" description="Source-policy capability matrix." />} />
      <Route path="/setup" element={<PlaceholderPage id="setup" title="Setup" ownerSlice="S10" description="First-run / environment configuration." />} />

      <Route path="*" element={<Navigate to={LANDING_PATH} replace />} />
    </Routes>
  );
}
