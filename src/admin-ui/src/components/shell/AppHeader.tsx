import {
  AppBar,
  Badge,
  Box,
  Chip,
  IconButton,
  Stack,
  Toolbar,
  Tooltip,
} from "@mui/material";
import MenuIcon from "@mui/icons-material/Menu";
import LightModeIcon from "@mui/icons-material/LightMode";
import DarkModeIcon from "@mui/icons-material/DarkMode";
import ViewComfyIcon from "@mui/icons-material/ViewComfy";
import ViewCompactIcon from "@mui/icons-material/ViewCompact";
import NotificationsIcon from "@mui/icons-material/Notifications";
import WifiIcon from "@mui/icons-material/Wifi";
import LogoutIcon from "@mui/icons-material/Logout";
import { observer } from "mobx-react-lite";
import { useNavigate } from "react-router-dom";
import { HeaderSearch } from "@/components/shell/HeaderSearch";
import type { AuthStore } from "@/stores/root";
import type { PrefStore } from "@/stores/pref.store";
import { LOGIN_PATH } from "@/app/routes";

/**
 * Global app header (per A1 H6: header globals own here, not in
 * screen slices). Composition per design brief §4:
 *  - mobile menu toggle (mobile-only)
 *  - smart search
 *  - alert badge (live count from store — S7 wires the source)
 *  - connection status indicator (S3 wires the source)
 *  - env tag (mainnet / testnet / dev)
 *  - theme mode + density toggles
 *  - logout
 */
export const AppHeader = observer(function AppHeader({
  auth,
  prefs,
  alertCount,
  connection,
  env,
  onToggleDrawer,
}: {
  auth: AuthStore;
  prefs: PrefStore;
  alertCount: number;
  connection: "online" | "offline" | "stale";
  env: string;
  onToggleDrawer: () => void;
}) {
  const navigate = useNavigate();
  const handleLogout = () => {
    auth.signOutSynthetic();
    navigate(LOGIN_PATH);
  };

  return (
    <AppBar position="sticky">
      {/* S2-audit M2: responsive composition.
          xs/sm: menu + compact search + alert + theme + logout only;
                 connection chip, env tag, and density toggle hidden.
          md+:   full stack as designed.
          Toolbar + search container both `minWidth: 0` so the
          search can shrink instead of overflowing the row. */}
      <Toolbar sx={{ gap: 1, minWidth: 0 }}>
        <IconButton
          aria-label="open navigation drawer"
          onClick={onToggleDrawer}
          edge="start"
          sx={{ display: { xs: "inline-flex", md: "none" } }}
        >
          <MenuIcon />
        </IconButton>

        <Box sx={{ flex: 1, minWidth: 0 }}>
          <HeaderSearch />
        </Box>

        <Stack
          direction="row"
          spacing={1}
          alignItems="center"
          sx={{ flexShrink: 0 }}
        >
          <Tooltip title={`SignalR ${connection}`}>
            <Chip
              icon={<WifiIcon fontSize="small" />}
              label={connection}
              size="small"
              color={connection === "online" ? "success" : connection === "stale" ? "warning" : "error"}
              variant="outlined"
              sx={{ display: { xs: "none", md: "inline-flex" } }}
            />
          </Tooltip>

          <Chip
            label={env}
            size="small"
            variant="outlined"
            sx={{ display: { xs: "none", sm: "inline-flex" } }}
          />

          <Tooltip title={`${alertCount} active alert${alertCount === 1 ? "" : "s"}`}>
            <IconButton onClick={() => navigate("/alerts")} aria-label="alerts">
              <Badge badgeContent={alertCount} color="error" overlap="circular">
                <NotificationsIcon />
              </Badge>
            </IconButton>
          </Tooltip>

          <Tooltip title={`Theme: ${prefs.mode}`}>
            <IconButton onClick={prefs.toggleMode} aria-label="toggle theme mode">
              {prefs.mode === "dark" ? <LightModeIcon /> : <DarkModeIcon />}
            </IconButton>
          </Tooltip>

          <Tooltip title={`Density: ${prefs.density}`}>
            <IconButton
              onClick={prefs.toggleDensity}
              aria-label="toggle density"
              sx={{ display: { xs: "none", md: "inline-flex" } }}
            >
              {prefs.density === "comfortable" ? <ViewCompactIcon /> : <ViewComfyIcon />}
            </IconButton>
          </Tooltip>

          <Tooltip title={auth.user?.name ? `Signed in as ${auth.user.name}` : "Logout"}>
            <IconButton onClick={handleLogout} aria-label="logout">
              <LogoutIcon />
            </IconButton>
          </Tooltip>
        </Stack>
      </Toolbar>
    </AppBar>
  );
});
