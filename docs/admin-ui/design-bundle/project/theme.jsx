// theme.jsx — Consigliere Admin design tokens, shaped as a MUI createTheme({...}) config.
// Both palettes ship together; the host flips `palette.mode` via a MobX store and the
// `density` rule flips `MuiDataGrid.defaultProps.density` plus a custom `--row-height`.

const ConsigliereThemeConfig = {
  // ─── PALETTE ────────────────────────────────────────────────────────────────
  // Both palettes are emitted; consumer chooses via createTheme({ palette: { mode } }).
  palette: {
    light: {
      mode: 'light',
      primary:   { main: '#3D5AFE', light: '#738FFE', dark: '#0031CA', contrastText: '#FFFFFF' },
      secondary: { main: '#00BFA5', light: '#5DF2D6', dark: '#008E76', contrastText: '#001210' },
      // Severity scale — matches the four MUI severities used by Snackbar+Alert+Chip color.
      severity: {
        info:    { main: '#0288D1', bg: '#E3F2FD', contrastText: '#FFFFFF' },
        success: { main: '#2E7D32', bg: '#E8F5E9', contrastText: '#FFFFFF' },
        warning: { main: '#ED6C02', bg: '#FFF4E5', contrastText: '#FFFFFF' },
        error:   { main: '#D32F2F', bg: '#FDECEA', contrastText: '#FFFFFF' },
      },
      // 5-stop gradient for the 0-100 peer score (P2P Pool screen). Stops at 0/25/50/75/100.
      score: { s0: '#D32F2F', s25: '#F57C00', s50: '#FBC02D', s75: '#7CB342', s100: '#2E7D32' },
      background: { default: '#F4F6F8', paper: '#FFFFFF', elev1: '#FFFFFF', elev2: '#F9FAFB' },
      text: { primary: 'rgba(0,0,0,0.87)', secondary: 'rgba(0,0,0,0.6)', disabled: 'rgba(0,0,0,0.38)' },
      divider: 'rgba(0,0,0,0.12)',
      action: { hover: 'rgba(0,0,0,0.04)', selected: 'rgba(61,90,254,0.08)' },
    },
    dark: {
      mode: 'dark',
      primary:   { main: '#8C9EFF', light: '#C3CEFF', dark: '#5B6BCB', contrastText: '#0E1116' },
      secondary: { main: '#26DFC6', light: '#7CF2DF', dark: '#00A48E', contrastText: '#001210' },
      severity: {
        info:    { main: '#29B6F6', bg: 'rgba(41,182,246,0.12)',  contrastText: '#0E1116' },
        success: { main: '#66BB6A', bg: 'rgba(102,187,106,0.12)', contrastText: '#0E1116' },
        warning: { main: '#FFA726', bg: 'rgba(255,167,38,0.12)',  contrastText: '#0E1116' },
        error:   { main: '#F44336', bg: 'rgba(244,67,54,0.12)',   contrastText: '#FFFFFF' },
      },
      score: { s0: '#EF5350', s25: '#FFA726', s50: '#FFEE58', s75: '#9CCC65', s100: '#66BB6A' },
      // Slightly deeper than MUI dark default (#121212) — chosen for always-on monitor legibility.
      background: { default: '#0E1116', paper: '#161B22', elev1: '#1C2128', elev2: '#22272E' },
      text: { primary: 'rgba(255,255,255,0.92)', secondary: 'rgba(255,255,255,0.62)', disabled: 'rgba(255,255,255,0.36)' },
      divider: 'rgba(255,255,255,0.1)',
      action: { hover: 'rgba(255,255,255,0.06)', selected: 'rgba(140,158,255,0.16)' },
    },
  },

  // ─── TYPOGRAPHY ─────────────────────────────────────────────────────────────
  typography: {
    fontFamily: '"Roboto","Helvetica","Arial",sans-serif',
    // Defensible deviation from Roboto-only: a separate code stack for hashes/hex/config-keys.
    // Used as `<Typography variant="code">` once registered via `typography.code` below.
    codeFontFamily: '"JetBrains Mono","Roboto Mono",ui-monospace,monospace',
    h1:       { fontWeight: 300, fontSize: '3.75rem', lineHeight: 1.167, letterSpacing: '-0.5px' },
    h2:       { fontWeight: 300, fontSize: '3rem',    lineHeight: 1.2,   letterSpacing: '-0.25px' },
    h3:       { fontWeight: 400, fontSize: '2.125rem',lineHeight: 1.235, letterSpacing: '0px' },
    h4:       { fontWeight: 500, fontSize: '1.5rem',  lineHeight: 1.334, letterSpacing: '0.15px' },
    h5:       { fontWeight: 500, fontSize: '1.25rem', lineHeight: 1.6,   letterSpacing: '0.15px' },
    h6:       { fontWeight: 500, fontSize: '1rem',    lineHeight: 1.6,   letterSpacing: '0.15px' },
    subtitle1:{ fontWeight: 400, fontSize: '1rem',    lineHeight: 1.75,  letterSpacing: '0.15px' },
    subtitle2:{ fontWeight: 500, fontSize: '0.875rem',lineHeight: 1.57,  letterSpacing: '0.1px' },
    body1:    { fontWeight: 400, fontSize: '1rem',    lineHeight: 1.5,   letterSpacing: '0.15px' },
    body2:    { fontWeight: 400, fontSize: '0.875rem',lineHeight: 1.43,  letterSpacing: '0.15px' },
    caption:  { fontWeight: 400, fontSize: '0.75rem', lineHeight: 1.66,  letterSpacing: '0.4px' },
    overline: { fontWeight: 500, fontSize: '0.75rem', lineHeight: 2.66,  letterSpacing: '1px', textTransform: 'uppercase' },
    // Custom variant for txids, hashes, base58 addresses, hot-reload config keys.
    code:     { fontFamily: '"JetBrains Mono","Roboto Mono",ui-monospace,monospace', fontWeight: 400, fontSize: '0.8125rem', lineHeight: 1.5, letterSpacing: '0px' },
  },

  // ─── SHAPE + SPACING ────────────────────────────────────────────────────────
  shape: { borderRadius: 8 },          // upgrade from MUI default 4 — modern observability-tool feel.
  spacing: 8,                          // MUI default; do not override.
  // Custom density token consumed by non-grid rows (cards, lists). DataGrid uses its own `density` prop.
  rowHeight: { comfortable: 52, dense: 36 },
  // Layout constants — match MUI defaults exactly so engineers can lift them.
  layout: { appBarHeight: { desktop: 64, mobile: 56 }, drawerWidth: 240 },

  // ─── COMPONENTS ─────────────────────────────────────────────────────────────
  components: {
    MuiDataGrid: { defaultProps: { density: 'standard' /* | 'compact' on dense toggle */ } },
    MuiAppBar:   { defaultProps: { color: 'transparent', elevation: 0 }, styleOverrides: { root: { backdropFilter: 'blur(8px)', borderBottom: '1px solid var(--divider)' } } },
    MuiCard:     { defaultProps: { elevation: 0, variant: 'outlined' } },
    MuiChip:     { defaultProps: { size: 'small' } },
    MuiButton:   { defaultProps: { disableElevation: true } },
  },

  // ─── ZINDEX ─────────────────────────────────────────────────────────────────
  // Stick with MUI defaults (drawer 1200, appBar 1100, snackbar 1400, modal 1300).
};

Object.assign(window, { ConsigliereThemeConfig });
