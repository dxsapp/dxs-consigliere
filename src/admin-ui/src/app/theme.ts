/**
 * Consigliere Admin design tokens, shaped as a MUI createTheme({...})
 * factory. Ported 1:1 from
 * `docs/admin-ui/design-bundle/project/theme.jsx`.
 *
 * Both palettes ship together; the host flips `palette.mode` via the
 * MobX pref store. Density flips MuiDataGrid `density` plus the
 * custom `rowHeight` token consumed by non-grid rows (cards, lists).
 *
 * Two designer deviations from the workspace baseline are accepted
 * (master.md §"Product Decision"):
 *  - `shape.borderRadius: 8` (vs MUI default 4)
 *  - `typography.code` uses JetBrains Mono (vs Roboto-only)
 */
import { createTheme, type Theme, type ThemeOptions } from "@mui/material/styles";
import "./theme-augmentation";

export type ThemeMode = "light" | "dark";
export type Density = "comfortable" | "dense";

// ─── shared (mode-independent) ────────────────────────────────────────────────

/** Monospace stack used by the `typography.code` variant; also
 *  exported for inline `<Box sx={{ fontFamily: codeFontFamily }}>`
 *  usage (e.g. DataGrid renderCell). Mirrors the design bundle's
 *  `typography.codeFontFamily` token. */
export const codeFontFamily =
  '"JetBrains Mono","Roboto Mono",ui-monospace,monospace';

const sharedTypography: NonNullable<ThemeOptions["typography"]> = {
  fontFamily: '"Roboto","Helvetica","Arial",sans-serif',
  h1: { fontWeight: 300, fontSize: "3.75rem", lineHeight: 1.167, letterSpacing: "-0.5px" },
  h2: { fontWeight: 300, fontSize: "3rem", lineHeight: 1.2, letterSpacing: "-0.25px" },
  h3: { fontWeight: 400, fontSize: "2.125rem", lineHeight: 1.235, letterSpacing: "0px" },
  h4: { fontWeight: 500, fontSize: "1.5rem", lineHeight: 1.334, letterSpacing: "0.15px" },
  h5: { fontWeight: 500, fontSize: "1.25rem", lineHeight: 1.6, letterSpacing: "0.15px" },
  h6: { fontWeight: 500, fontSize: "1rem", lineHeight: 1.6, letterSpacing: "0.15px" },
  subtitle1: { fontWeight: 400, fontSize: "1rem", lineHeight: 1.75, letterSpacing: "0.15px" },
  subtitle2: { fontWeight: 500, fontSize: "0.875rem", lineHeight: 1.57, letterSpacing: "0.1px" },
  body1: { fontWeight: 400, fontSize: "1rem", lineHeight: 1.5, letterSpacing: "0.15px" },
  body2: { fontWeight: 400, fontSize: "0.875rem", lineHeight: 1.43, letterSpacing: "0.15px" },
  caption: { fontWeight: 400, fontSize: "0.75rem", lineHeight: 1.66, letterSpacing: "0.4px" },
  overline: {
    fontWeight: 500,
    fontSize: "0.75rem",
    lineHeight: 2.66,
    letterSpacing: "1px",
    textTransform: "uppercase",
  },
  code: {
    fontFamily: codeFontFamily,
    fontWeight: 400,
    fontSize: "0.8125rem",
    lineHeight: 1.5,
    letterSpacing: "0px",
  },
};

const sharedShape = { borderRadius: 8 };
const sharedSpacing = 8;
const sharedRowHeight = { comfortable: 52, dense: 36 };
const sharedLayout = {
  appBarHeight: { desktop: 64, mobile: 56 },
  drawerWidth: 240,
};

// ─── palettes ────────────────────────────────────────────────────────────────

const lightPalette: NonNullable<ThemeOptions["palette"]> = {
  mode: "light",
  primary: { main: "#3D5AFE", light: "#738FFE", dark: "#0031CA", contrastText: "#FFFFFF" },
  secondary: { main: "#00BFA5", light: "#5DF2D6", dark: "#008E76", contrastText: "#001210" },
  severity: {
    info: { main: "#0288D1", bg: "#E3F2FD", contrastText: "#FFFFFF" },
    success: { main: "#2E7D32", bg: "#E8F5E9", contrastText: "#FFFFFF" },
    warning: { main: "#ED6C02", bg: "#FFF4E5", contrastText: "#FFFFFF" },
    error: { main: "#D32F2F", bg: "#FDECEA", contrastText: "#FFFFFF" },
  },
  score: { s0: "#D32F2F", s25: "#F57C00", s50: "#FBC02D", s75: "#7CB342", s100: "#2E7D32" },
  background: { default: "#F4F6F8", paper: "#FFFFFF", elev1: "#FFFFFF", elev2: "#F9FAFB" },
  text: {
    primary: "rgba(0,0,0,0.87)",
    secondary: "rgba(0,0,0,0.6)",
    disabled: "rgba(0,0,0,0.38)",
  },
  divider: "rgba(0,0,0,0.12)",
  action: { hover: "rgba(0,0,0,0.04)", selected: "rgba(61,90,254,0.08)" },
};

const darkPalette: NonNullable<ThemeOptions["palette"]> = {
  mode: "dark",
  primary: { main: "#8C9EFF", light: "#C3CEFF", dark: "#5B6BCB", contrastText: "#0E1116" },
  secondary: { main: "#26DFC6", light: "#7CF2DF", dark: "#00A48E", contrastText: "#001210" },
  severity: {
    info: { main: "#29B6F6", bg: "rgba(41,182,246,0.12)", contrastText: "#0E1116" },
    success: { main: "#66BB6A", bg: "rgba(102,187,106,0.12)", contrastText: "#0E1116" },
    warning: { main: "#FFA726", bg: "rgba(255,167,38,0.12)", contrastText: "#0E1116" },
    error: { main: "#F44336", bg: "rgba(244,67,54,0.12)", contrastText: "#FFFFFF" },
  },
  score: { s0: "#EF5350", s25: "#FFA726", s50: "#FFEE58", s75: "#9CCC65", s100: "#66BB6A" },
  // Slightly deeper than MUI dark default (#121212) — chosen for
  // always-on monitor legibility per the design bundle.
  background: { default: "#0E1116", paper: "#161B22", elev1: "#1C2128", elev2: "#22272E" },
  text: {
    primary: "rgba(255,255,255,0.92)",
    secondary: "rgba(255,255,255,0.62)",
    disabled: "rgba(255,255,255,0.36)",
  },
  divider: "rgba(255,255,255,0.1)",
  action: { hover: "rgba(255,255,255,0.06)", selected: "rgba(140,158,255,0.16)" },
};

// ─── factory ─────────────────────────────────────────────────────────────────

/** Build a fully-typed MUI theme for the given (mode, density) pair. */
export function buildTheme(mode: ThemeMode, density: Density): Theme {
  const palette = mode === "light" ? lightPalette : darkPalette;
  return createTheme({
    palette,
    typography: sharedTypography,
    shape: sharedShape,
    spacing: sharedSpacing,
    rowHeight: sharedRowHeight,
    layout: sharedLayout,
    components: {
      MuiDataGrid: {
        defaultProps: { density: density === "dense" ? "compact" : "standard" },
      },
      MuiAppBar: {
        defaultProps: { color: "transparent", elevation: 0 },
        // S1-audit L1 intentional deviation: the design bundle's
        // theme.jsx writes `borderBottom: '1px solid var(--divider)'`,
        // assuming a CSS-variable wired separately. We resolve the
        // divider colour from the palette at theme-construction time
        // so the AppBar is correct without any extra CSS-var plumbing.
        // Documented here as an intentional implementation deviation.
        styleOverrides: {
          root: {
            backdropFilter: "blur(8px)",
            borderBottom: `1px solid ${(palette as { divider?: string }).divider ?? "rgba(0,0,0,0.12)"}`,
          },
        },
      },
      MuiCard: { defaultProps: { elevation: 0, variant: "outlined" } },
      MuiChip: { defaultProps: { size: "small" } },
      MuiButton: { defaultProps: { disableElevation: true } },
    },
  });
}
