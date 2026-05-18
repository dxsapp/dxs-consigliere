import { BrowserRouter, Navigate, Route, Routes } from "react-router-dom";
import { lazy, Suspense, useEffect, useState } from "react";
import { LoginPage } from "@/screens/login/LoginPage";
import { RootStore } from "@/stores/root";
import { hydratePrefStore } from "@/stores/pref.store";
import { ThemeProvider } from "@/app/ThemeProvider";

// S1-audit L3: dev-only theme demo route is lazy-loaded so its
// MUI primitives + icon imports don't fatten the production shell.
const DevThemeDemoPage = lazy(() =>
  import("@/screens/dev-theme-demo/DevThemeDemoPage").then((m) => ({
    default: m.DevThemeDemoPage,
  }))
);

const root = new RootStore();

// S0 placeholder authenticated landing — S2 swaps this for the
// AppBar + Drawer shell and real routes.
function AuthedPlaceholder() {
  return (
    <div style={{ padding: 32 }}>
      <h1>Consigliere Admin</h1>
      <p>S1 scaffold. Shell + routes ship in S2.</p>
      <p>
        Visit <a href="/dev/theme-demo">/dev/theme-demo</a> to verify the design tokens.
      </p>
    </div>
  );
}

export function App() {
  const [hydrated, setHydrated] = useState(false);

  useEffect(() => {
    // S1-audit M1: hydratePrefStore is idempotent (StrictMode double-
    // mount safe) and failure-safe (storage error resets defaults +
    // marks hydrated). No additional catch needed here.
    void hydratePrefStore(root.prefs).then(() => setHydrated(true));
  }, []);

  // Render nothing until prefs are hydrated to avoid a flash of
  // default-theme before the persisted choice loads.
  if (!hydrated) return null;

  return (
    <ThemeProvider prefs={root.prefs}>
      <BrowserRouter>
        <Routes>
          <Route path="/login" element={<LoginPage />} />
          <Route
            path="/dev/theme-demo"
            element={
              <Suspense fallback={null}>
                <DevThemeDemoPage prefs={root.prefs} />
              </Suspense>
            }
          />
          {/* S0: every authed route is a placeholder. S2 wires the
              real shell + per-screen routes. The 401 redirect from
              the API client (lib/api/client.ts) sends the user back
              to /login if their cookie session expired. */}
          <Route path="/" element={<AuthedPlaceholder />} />
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </BrowserRouter>
    </ThemeProvider>
  );
}
