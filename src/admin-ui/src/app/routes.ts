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
 */

export type RouteSection = "operator" | "system";

export interface NavRoute {
  /** Stable id for the sidebar key + tests. */
  id: string;
  /** Display label in the drawer. */
  label: string;
  /** Material Symbols icon name (string) for the leading icon. */
  icon: string;
  /** Router path (no params). */
  path: string;
  /** Section grouping in the drawer. */
  section: RouteSection;
  /** When true, drawer prefix-matches the path (so `/transactions/:txid` still highlights "Transactions"). */
  prefix?: boolean;
}

export const NAV_ROUTES: NavRoute[] = [
  // Operator section.
  { id: "dashboard", label: "Dashboard", icon: "dashboard", path: "/dashboard", section: "operator" },
  { id: "transactions", label: "Transactions", icon: "swap_horiz", path: "/transactions", section: "operator", prefix: true },
  { id: "broadcast-queue", label: "Broadcast Queue", icon: "cell_tower", path: "/broadcast-queue", section: "operator" },
  { id: "addresses", label: "Addresses", icon: "account_balance_wallet", path: "/addresses", section: "operator", prefix: true },
  { id: "tokens", label: "Tokens", icon: "token", path: "/tokens", section: "operator", prefix: true },
  { id: "alerts", label: "Alerts", icon: "notifications", path: "/alerts", section: "operator" },

  // System section.
  { id: "p2p", label: "P2P Pool", icon: "hub", path: "/p2p", section: "system" },
  { id: "source-metrics", label: "Source Metrics", icon: "insights", path: "/metrics/sources", section: "system" },
  { id: "headers", label: "Headers Chain", icon: "linear_scale", path: "/headers", section: "system" },
  { id: "broadcast-inspector", label: "Broadcast Inspector", icon: "biotech", path: "/broadcast-inspector", section: "system" },
  { id: "configuration", label: "Configuration", icon: "settings", path: "/configuration", section: "system" },
  { id: "logs", label: "Logs / Raw", icon: "data_object", path: "/logs", section: "system" },
  { id: "providers", label: "Providers", icon: "cloud", path: "/providers", section: "system" },
  { id: "setup", label: "Setup", icon: "tune", path: "/setup", section: "system" },
];

export const OPERATOR_ROUTES = NAV_ROUTES.filter((r) => r.section === "operator");
export const SYSTEM_ROUTES = NAV_ROUTES.filter((r) => r.section === "system");

export const LANDING_PATH = "/dashboard";
export const LOGIN_PATH = "/login";
