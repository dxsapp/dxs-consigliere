import { Box } from "@mui/material";
import { observer } from "mobx-react-lite";
import { useState, type ReactNode } from "react";
import { AppDrawer } from "@/components/shell/AppDrawer";
import { AppHeader } from "@/components/shell/AppHeader";
import type { AuthStore } from "@/stores/root";
import type { PrefStore } from "@/stores/pref.store";
import type { ShellStore } from "@/stores/shell.store";

/**
 * Authenticated app shell — AppBar + Drawer + scrollable content.
 * Wraps every authed route (see AppGuardedRoutes).
 *
 * S2-audit L4: header globals (alert count + connection status) are
 * read from the ShellStore observable slice, not hard-coded here.
 * S3 wires real signals; S7 wires the alert-count source.
 */
export const AppShell = observer(function AppShell({
  auth,
  prefs,
  shell,
  env,
  children,
}: {
  auth: AuthStore;
  prefs: PrefStore;
  shell: ShellStore;
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
          alertCount={shell.alertCount}
          connection={shell.connection}
          onToggleDrawer={() => setMobileOpen((v) => !v)}
        />
        <Box component="main" sx={{ flex: 1, p: { xs: 2, md: 3 }, minWidth: 0 }}>
          {children}
        </Box>
      </Box>
    </Box>
  );
});
