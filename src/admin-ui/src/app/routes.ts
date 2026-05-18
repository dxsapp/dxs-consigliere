import AccountBalanceWalletIcon from "@mui/icons-material/AccountBalanceWallet";
import BiotechIcon from "@mui/icons-material/Biotech";
import CellTowerIcon from "@mui/icons-material/CellTower";
import CloudIcon from "@mui/icons-material/Cloud";
import DashboardIcon from "@mui/icons-material/Dashboard";
import DataObjectIcon from "@mui/icons-material/DataObject";
import HubIcon from "@mui/icons-material/Hub";
import InsightsIcon from "@mui/icons-material/Insights";
import LinearScaleIcon from "@mui/icons-material/LinearScale";
import NotificationsIcon from "@mui/icons-material/Notifications";
import SettingsIcon from "@mui/icons-material/Settings";
import SwapHorizIcon from "@mui/icons-material/SwapHoriz";
import TokenIcon from "@mui/icons-material/Token";
import TuneIcon from "@mui/icons-material/Tune";
import type { ElementType } from "react";

/**
 * Route inventory. Single source of truth for sidebar nav + router
 * wiring. Per Core Rule §1 (workspace-stack-only) the nav lives in
 * MUI primitives — see AppDrawer.
 *
 * Two visually-separated sections (per design brief §4):
 *  - Operator  — 6 default-visible routes
 *  - System    — 8 advanced-visible routes (DEV chip in the drawer)
 *
 * Detail routes (`/transactions/:txid`, `/addresses/:address`,
 * `/tokens/:tokenId`) are not in the sidebar; they're reached via
 * the header search or by drilling in from a list.
 *
 * Icons (S2-audit L2): typed MUI icon components, not Material
 * Symbols font strings. Tree-shakeable + type-safe. The Material
 * Symbols icon font in `index.html` is reserved for the demo +
 * brand glyphs (theme-demo + AppDrawer brand mark).
 */

export type RouteSection = "operator" | "system";

export interface NavRoute {
  /** Stable id for the sidebar key + tests. */
  id: string;
  /** Display label in the drawer. */
  label: string;
  /** MUI icon component for the leading icon. */
  icon: ElementType;
  /** Router path (no params). */
  path: string;
  /** Section grouping in the drawer. */
  section: RouteSection;
  /**
   * When true, the drawer's NavLink uses prefix-matching so
   * detail routes like `/transactions/:txid` still highlight
   * the parent "Transactions" entry (S2-audit M1 fix).
   */
  prefix?: boolean;
}

export const NAV_ROUTES: NavRoute[] = [
  // Operator section.
  { id: "dashboard", label: "Dashboard", icon: DashboardIcon, path: "/dashboard", section: "operator" },
  { id: "transactions", label: "Transactions", icon: SwapHorizIcon, path: "/transactions", section: "operator", prefix: true },
  { id: "broadcast-queue", label: "Broadcast Queue", icon: CellTowerIcon, path: "/broadcast-queue", section: "operator" },
  { id: "addresses", label: "Addresses", icon: AccountBalanceWalletIcon, path: "/addresses", section: "operator", prefix: true },
  { id: "tokens", label: "Tokens", icon: TokenIcon, path: "/tokens", section: "operator", prefix: true },
  { id: "alerts", label: "Alerts", icon: NotificationsIcon, path: "/alerts", section: "operator" },

  // System section.
  { id: "p2p", label: "P2P Pool", icon: HubIcon, path: "/p2p", section: "system" },
  { id: "source-metrics", label: "Source Metrics", icon: InsightsIcon, path: "/metrics/sources", section: "system" },
  { id: "headers", label: "Headers Chain", icon: LinearScaleIcon, path: "/headers", section: "system" },
  { id: "broadcast-inspector", label: "Broadcast Inspector", icon: BiotechIcon, path: "/broadcast-inspector", section: "system" },
  { id: "configuration", label: "Configuration", icon: SettingsIcon, path: "/configuration", section: "system" },
  { id: "logs", label: "Logs / Raw", icon: DataObjectIcon, path: "/logs", section: "system" },
  { id: "providers", label: "Providers", icon: CloudIcon, path: "/providers", section: "system" },
  { id: "setup", label: "Setup", icon: TuneIcon, path: "/setup", section: "system" },
];

export const OPERATOR_ROUTES = NAV_ROUTES.filter((r) => r.section === "operator");
export const SYSTEM_ROUTES = NAV_ROUTES.filter((r) => r.section === "system");

export const LANDING_PATH = "/dashboard";
export const LOGIN_PATH = "/login";
