import { BrowserRouter, Navigate, Route, Routes } from "react-router-dom";
import { useEffect, useState } from "react";
import { LoginPage } from "@/screens/login/LoginPage";
import { DevThemeDemoPage } from "@/screens/dev-theme-demo/DevThemeDemoPage";
import { RootStore } from "@/stores/root";
import { hydratePrefStore } from "@/stores/pref.store";
import { ThemeProvider } from "@/app/ThemeProvider";

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
          <Route path="/dev/theme-demo" element={<DevThemeDemoPage prefs={root.prefs} />} />
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
