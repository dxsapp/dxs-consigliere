import { useState } from "react";
import { Outlet, NavLink, useNavigate } from "react-router-dom";
import { observer } from "mobx-react-lite";
import {
  Box,
  Drawer,
  List,
  ListItem,
  ListItemButton,
  ListItemIcon,
  ListItemText,
  Toolbar,
  Typography,
  Divider,
  IconButton,
  Tooltip,
} from "@mui/material";
import DashboardOutlinedIcon from "@mui/icons-material/DashboardOutlined";
import AccountBalanceWalletOutlinedIcon from "@mui/icons-material/AccountBalanceWalletOutlined";
import TokenOutlinedIcon from "@mui/icons-material/TokenOutlined";
import MemoryOutlinedIcon from "@mui/icons-material/MemoryOutlined";
import StorageOutlinedIcon from "@mui/icons-material/StorageOutlined";
import WarningAmberOutlinedIcon from "@mui/icons-material/WarningAmberOutlined";
import LogoutOutlinedIcon from "@mui/icons-material/LogoutOutlined";
import { authStore } from "@/stores/auth.store";

const DRAWER_WIDTH = 220;

const NAV_ITEMS = [
  { label: "Dashboard", path: "/", icon: <DashboardOutlinedIcon fontSize="small" />, exact: true },
  { label: "Addresses", path: "/addresses", icon: <AccountBalanceWalletOutlinedIcon fontSize="small" /> },
  { label: "Tokens", path: "/tokens", icon: <TokenOutlinedIcon fontSize="small" /> },
  { label: "Runtime", path: "/runtime", icon: <MemoryOutlinedIcon fontSize="small" /> },
  { label: "Storage", path: "/storage", icon: <StorageOutlinedIcon fontSize="small" /> },
  { label: "Findings", path: "/findings", icon: <WarningAmberOutlinedIcon fontSize="small" /> },
];

export const AppShell = observer(function AppShell() {
  const navigate = useNavigate();
  const [loggingOut, setLoggingOut] = useState(false);

  const handleLogout = async () => {
    setLoggingOut(true);
    await authStore.logout();
    navigate("/login", { replace: true });
  };

  return (
    <Box sx={{ display: "flex", minHeight: "100vh" }}>
      {/* Sidebar */}
      <Drawer
        variant="permanent"
        sx={{
          width: DRAWER_WIDTH,
          flexShrink: 0,
          "& .MuiDrawer-paper": {
            width: DRAWER_WIDTH,
            boxSizing: "border-box",
            display: "flex",
            flexDirection: "column",
          },
        }}
      >
        {/* Logo / brand */}
        <Toolbar
          sx={{
            px: 2,
            borderBottom: "1px solid",
            borderColor: "divider",
            minHeight: "56px !important",
          }}
        >
          <Typography
            variant="h6"
            noWrap
            sx={{ fontWeight: 700, letterSpacing: "-0.02em", color: "primary.light" }}
          >
            Consigliere
          </Typography>
        </Toolbar>

        {/* Nav */}
        <List sx={{ flex: 1, pt: 1, px: 1 }}>
          {NAV_ITEMS.map((item) => (
            <ListItem key={item.path} disablePadding sx={{ mb: 0.25 }}>
              <ListItemButton
                component={NavLink}
                to={item.path}
                end={item.exact}
                sx={{
                  borderRadius: 1.5,
                  py: 0.75,
                  px: 1.5,
                  color: "text.secondary",
                  "&.active": {
                    color: "primary.light",
                    bgcolor: "rgba(99, 102, 241, 0.12)",
                  },
                  "&:hover:not(.active)": {
                    bgcolor: "rgba(255,255,255,0.04)",
                    color: "text.primary",
                  },
                }}
              >
                <ListItemIcon
                  sx={{
                    minWidth: 32,
                    color: "inherit",
                  }}
                >
                  {item.icon}
                </ListItemIcon>
                <ListItemText
                  primary={item.label}
                  primaryTypographyProps={{ fontSize: "0.875rem", fontWeight: 500 }}
                />
              </ListItemButton>
            </ListItem>
          ))}
        </List>

        {/* Footer: user + logout */}
        <Divider />
        <Box
          sx={{
            px: 2,
            py: 1.5,
            display: "flex",
            alignItems: "center",
            justifyContent: "space-between",
          }}
        >
          <Typography variant="body2" noWrap sx={{ color: "text.disabled", fontSize: "0.75rem" }}>
            {authStore.username ?? "admin"}
          </Typography>
          {authStore.isAuthEnabled && (
            <Tooltip title="Logout">
              <IconButton
                size="small"
                onClick={handleLogout}
                disabled={loggingOut}
                sx={{ color: "text.disabled", "&:hover": { color: "text.secondary" } }}
              >
                <LogoutOutlinedIcon fontSize="small" />
              </IconButton>
            </Tooltip>
          )}
        </Box>
      </Drawer>

      {/* Main content */}
      <Box
        component="main"
        sx={{
          flex: 1,
          minWidth: 0,
          bgcolor: "background.default",
          p: 3,
        }}
      >
        <Outlet />
      </Box>
    </Box>
  );
});
