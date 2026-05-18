import { Box } from "@mui/material";
import { observer } from "mobx-react-lite";
import { useState, type ReactNode } from "react";
import { AppDrawer } from "@/components/shell/AppDrawer";
import { AppHeader } from "@/components/shell/AppHeader";
import type { AuthStore } from "@/stores/root";
import type { PrefStore } from "@/stores/pref.store";

/**
 * Authenticated app shell — AppBar + Drawer + scrollable content.
 * Wraps every authed route (see AppGuardedRoutes).
 *
 * Connection + alert-count are passed in as props (S2: placeholder
 * values; S3 wires the real signals from the SignalR client + the
 * alert store).
 */
export const AppShell = observer(function AppShell({
  auth,
  prefs,
  env,
  children,
}: {
  auth: AuthStore;
  prefs: PrefStore;
  env: string;
  children: ReactNode;
}) {
  const [mobileOpen, setMobileOpen] = useState(false);
  return (
    <Box sx={{ display: "flex", minHeight: "100vh" }}>
      <AppDrawer mobileOpen={mobileOpen} onClose={() => setMobileOpen(false)} />
      <Box sx={{ flex: 1, display: "flex", flexDirection: "column", minWidth: 0 }}>
        <AppHeader
          auth={auth}
          prefs={prefs}
          env={env}
          alertCount={0 /* S7 wires this from the alerts store */}
          connection={"online" /* S3 wires this from the SignalR client */}
          onToggleDrawer={() => setMobileOpen((v) => !v)}
        />
        <Box component="main" sx={{ flex: 1, p: { xs: 2, md: 3 }, minWidth: 0 }}>
          {children}
        </Box>
      </Box>
    </Box>
  );
});
