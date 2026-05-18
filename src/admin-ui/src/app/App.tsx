import { BrowserRouter, Navigate, Route, Routes } from "react-router-dom";
import { lazy, Suspense, useEffect, useState } from "react";
import { PlaceholderPage } from "@/screens/_placeholder/PlaceholderPage";
import { RootStore } from "@/stores/root";
import { hydratePrefStore } from "@/stores/pref.store";
import { ThemeProvider } from "@/app/ThemeProvider";
import { AuthGuard } from "@/app/AuthGuard";
import { AppShell } from "@/components/shell/AppShell";
import { LANDING_PATH, LOGIN_PATH } from "@/app/routes";

// S1-audit L3 + S2-audit (bundle headroom): dev-only theme demo
// AND the LoginPage are lazy-loaded so their MUI imports stay out
// of the authed-operator shell.
const DevThemeDemoPage = lazy(() =>
  import("@/screens/dev-theme-demo/DevThemeDemoPage").then((m) => ({
    default: m.DevThemeDemoPage,
  }))
);
const LoginPage = lazy(() =>
  import("@/screens/login/LoginPage").then((m) => ({ default: m.LoginPage }))
);

const root = new RootStore();

// Vite-injected env. Override via VITE_CONSIGLIERE_ENV at build time.
const ENV_LABEL = (import.meta.env.VITE_CONSIGLIERE_ENV as string | undefined) ?? "mainnet";

export function App() {
  const [hydrated, setHydrated] = useState(false);

  useEffect(() => {
    // S1-audit M1: hydratePrefStore is idempotent (StrictMode double-
    // mount safe) and failure-safe (storage error resets defaults +
    // marks hydrated). No additional catch needed here.
    void hydratePrefStore(root.prefs).then(() => setHydrated(true));
  }, []);

  if (!hydrated) return null;

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
                  <AuthedRoutes />
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
function AuthedRoutes() {
  return (
    <Routes>
      <Route path="/" element={<Navigate to={LANDING_PATH} replace />} />

      {/* Operator section. */}
      <Route path="/dashboard" element={<PlaceholderPage id="dashboard" title="Dashboard" ownerSlice="S4" description="Always-open system health + activity stream + search prompt." />} />
      <Route path="/transactions" element={<PlaceholderPage id="transactions" title="Transactions" ownerSlice="S5" description="Lookup + lifecycle viewer." />} />
      <Route path="/transactions/:txid" element={<PlaceholderPage id="transactions-detail" title="Transaction" ownerSlice="S5" description="Vertical stepper for the OutgoingTxState lifecycle." />} />
      <Route path="/broadcast-queue" element={<PlaceholderPage id="broadcast-queue" title="Broadcast Queue" ownerSlice="S6" description="3-column kanban with framer-motion state transitions." />} />
      <Route path="/addresses" element={<PlaceholderPage id="addresses" title="Addresses" ownerSlice="S5" description="Address lookup + state." />} />
      <Route path="/addresses/:address" element={<PlaceholderPage id="addresses-detail" title="Address" ownerSlice="S5" description="Per-address timeline." />} />
      <Route path="/tokens" element={<PlaceholderPage id="tokens" title="Tokens" ownerSlice="S5" description="DSTAS / native token lookup." />} />
      <Route path="/tokens/:tokenId" element={<PlaceholderPage id="tokens-detail" title="Token" ownerSlice="S5" description="Per-token timeline." />} />
      <Route path="/alerts" element={<PlaceholderPage id="alerts" title="Alerts" ownerSlice="S7" description="Active alerts + history journal (poll-delta toasts)." />} />

      {/* System section. */}
      <Route path="/p2p" element={<PlaceholderPage id="p2p" title="P2P Pool" ownerSlice="S8" description="Peers DataGrid with ScoreBar + /24 diversity donut." />} />
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
