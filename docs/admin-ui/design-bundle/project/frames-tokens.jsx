// frames-tokens.jsx — Design tokens, rendered visually + as a copy-pastable
// createTheme({...}) config. Both light + dark palettes shown side-by-side.

function TokensFrame({ width = 1440 }) {
  const palette = ConsigliereThemeConfig.palette.dark;
  const ctx = { p: palette, t: ConsigliereThemeConfig.typography, density: 'comfortable' };
  return (
    <ThemeCtx.Provider value={ctx}>
      <div style={{ width, background: palette.background.default, color: palette.text.primary, fontFamily: 'Roboto, sans-serif', padding: 40 }}>
        <div style={{ marginBottom: 32 }}>
          <Typography variant="overline" color="secondary">Design tokens</Typography>
          <Typography variant="h3" sx={{ fontWeight:400, marginTop: 4 }}>Consigliere theme</Typography>
          <Typography variant="body1" color="secondary" sx={{ marginTop: 8, maxWidth: 760 }}>
            Shaped 1:1 as a MUI <code style={{ fontFamily:'"JetBrains Mono",monospace' }}>createTheme({'{...}'})</code> config. Both palettes ship together; the toggle lives in the header IconButton and persists via mobx-persist-store. Density flips MuiDataGrid <code style={{ fontFamily:'"JetBrains Mono",monospace' }}>density</code> + a custom <code style={{ fontFamily:'"JetBrains Mono",monospace' }}>rowHeight</code> token.
          </Typography>
        </div>

        {/* Palette — two columns: light + dark */}
        <Typography variant="overline" color="secondary" sx={{ display:'block', marginBottom: 12 }}>Palette</Typography>
        <div style={{ display:'grid', gridTemplateColumns:'1fr 1fr', gap: 24, marginBottom: 40 }}>
          <PaletteColumn mode="light" />
          <PaletteColumn mode="dark" />
        </div>

        {/* Score gradient */}
        <Typography variant="overline" color="secondary" sx={{ display:'block', marginBottom: 12 }}>palette.score — 0-100 peer score gradient (P2P Pool)</Typography>
        <Card variant="outlined" sx={{ padding: 24, marginBottom: 40 }}>
          <div style={{ display:'flex', height: 56, borderRadius: 8, overflow:'hidden' }}>
            {['s0','s25','s50','s75','s100'].map((k,i) => (
              <div key={k} style={{ flex:1, background: palette.score[k], display:'flex', alignItems:'flex-end', justifyContent:'center', padding:'8px', color: i < 2 ? '#FFF' : '#0E1116', fontFamily:'"JetBrains Mono",monospace', fontSize:'0.75rem', fontWeight:600 }}>
                {k === 's0' ? '0' : k === 's25' ? '25' : k === 's50' ? '50' : k === 's75' ? '75' : '100'} · {palette.score[k]}
              </div>
            ))}
          </div>
          <div style={{ display:'flex', marginTop: 16, gap: 16, alignItems:'center' }}>
            <Typography variant="caption" color="secondary">Used by <code style={{ fontFamily:'"JetBrains Mono",monospace' }}>&lt;ScoreBar score=</code> cell on the P2P Pool DataGrid.</Typography>
          </div>
        </Card>

        {/* Typography scale */}
        <Typography variant="overline" color="secondary" sx={{ display:'block', marginBottom: 12 }}>Typography scale</Typography>
        <Card variant="outlined" sx={{ padding: 24, marginBottom: 40 }}>
          <Stack spacing={2.5}>
            <TypeRow variant="h1" label="h1 · 3.75rem / 300" sample="Consigliere" />
            <TypeRow variant="h2" label="h2 · 3rem / 300"    sample="System nominal" />
            <TypeRow variant="h3" label="h3 · 2.125rem / 400" sample="Broadcast Queue" />
            <TypeRow variant="h4" label="h4 · 1.5rem / 500"   sample="P2P Pool" />
            <TypeRow variant="h5" label="h5 · 1.25rem / 500"  sample="Active alerts" />
            <TypeRow variant="h6" label="h6 · 1rem / 500"     sample="Recent broadcasts" />
            <TypeRow variant="subtitle1" label="subtitle1 · 1rem / 400" sample="In-flight outgoing transactions" />
            <TypeRow variant="body1" label="body1 · 1rem / 400"         sample="The W3 lifecycle monitor will retry on next reconnect." />
            <TypeRow variant="body2" label="body2 · 0.875rem / 400"     sample="GET /api/admin/p2p/peers — live · sort by score asc" />
            <TypeRow variant="caption" label="caption · 0.75rem / 400"  sample="14:23:11 UTC · 4 min ago" />
            <TypeRow variant="overline" label="overline · 0.75rem / 500 / uppercase" sample="Per-source visibility" />
            <TypeRow variant="code"  label="code · 0.8125rem · JetBrains Mono (deviation: hex/txid/config-key surfaces)" sample="a91f3c12d80b9e4f5c6d7e8a9b0c1d2e3f4a5b6c7d8e9f0a1b2c3d4e5f6a7c4d" />
          </Stack>
        </Card>

        {/* Shape + spacing + density */}
        <div style={{ display:'grid', gridTemplateColumns:'1fr 1fr', gap: 24, marginBottom: 40 }}>
          <Card variant="outlined" sx={{ padding: 24 }}>
            <Typography variant="overline" color="secondary">Shape · spacing</Typography>
            <Stack spacing={2} sx={{ marginTop: 16 }}>
              <Stack direction="row" alignItems="center" spacing={2}>
                <Typography variant="caption" sx={{ width: 120, color: palette.text.secondary, fontFamily:'"JetBrains Mono",monospace' }}>borderRadius: 8</Typography>
                <div style={{ width: 56, height: 32, background: palette.primary.main, borderRadius: 8 }} />
                <Typography variant="caption" color="secondary">upgrade from MUI default 4 · modern observability-tool feel</Typography>
              </Stack>
              <Stack direction="row" alignItems="center" spacing={2}>
                <Typography variant="caption" sx={{ width: 120, color: palette.text.secondary, fontFamily:'"JetBrains Mono",monospace' }}>spacing: 8</Typography>
                <Stack direction="row" spacing={0.5}>
                  {[1,2,3,4,5].map(n => <div key={n} style={{ width: 8*n, height: 12, background: palette.secondary.main, borderRadius: 2 }} />)}
                </Stack>
                <Typography variant="caption" color="secondary">MUI default · do not override</Typography>
              </Stack>
            </Stack>
          </Card>
          <Card variant="outlined" sx={{ padding: 24 }}>
            <Typography variant="overline" color="secondary">Density — rowHeight token</Typography>
            <Stack spacing={2} sx={{ marginTop: 16 }}>
              <Stack direction="row" alignItems="center" spacing={2}>
                <Typography variant="caption" sx={{ width: 120, color: palette.text.secondary, fontFamily:'"JetBrains Mono",monospace' }}>comfortable: 52</Typography>
                <div style={{ flex:1, height: 52, background: palette.background.elev1, border:`1px solid ${palette.divider}`, borderRadius: 6, display:'flex', alignItems:'center', padding:'0 12px', fontSize:'0.875rem' }}>DataGrid standard row · ListItemButton non-dense</div>
              </Stack>
              <Stack direction="row" alignItems="center" spacing={2}>
                <Typography variant="caption" sx={{ width: 120, color: palette.text.secondary, fontFamily:'"JetBrains Mono",monospace' }}>dense: 36</Typography>
                <div style={{ flex:1, height: 36, background: palette.background.elev1, border:`1px solid ${palette.divider}`, borderRadius: 6, display:'flex', alignItems:'center', padding:'0 12px', fontSize:'0.8125rem' }}>DataGrid compact row · ListItemButton dense</div>
              </Stack>
              <Typography variant="caption" color="secondary">Flip via header IconButton → MobX store → DataGrid density prop + sx consumer reads var(--row-height).</Typography>
            </Stack>
          </Card>
        </div>

        {/* createTheme JSON */}
        <Typography variant="overline" color="secondary" sx={{ display:'block', marginBottom: 12 }}>createTheme({'{...}'}) config — drop-in</Typography>
        <div style={{ padding: 20, background: '#0B0E12', border:`1px solid ${palette.divider}`, borderRadius: 8, fontFamily:'"JetBrains Mono",monospace', fontSize:'0.75rem', lineHeight:1.7, color: palette.text.secondary, overflow:'auto' }}>
          <pre style={{ margin:0, fontFamily:'inherit', whiteSpace:'pre-wrap' }}>{themeCodeString()}</pre>
        </div>
      </div>
    </ThemeCtx.Provider>
  );
}

function PaletteColumn({ mode }) {
  const palette = ConsigliereThemeConfig.palette[mode];
  const { p: outer } = useTheme();
  return (
    <Card variant="outlined" sx={{ padding: 20, background: palette.background.default }}>
      <Stack direction="row" alignItems="center" spacing={1} sx={{ marginBottom: 16 }}>
        <Icon name={mode === 'dark' ? 'dark_mode' : 'light_mode'} size={18} sx={{ color: palette.text.primary }} />
        <Typography variant="h6" sx={{ color: palette.text.primary, fontSize:'1rem', fontWeight: 600 }}>palette.mode = '{mode}'</Typography>
      </Stack>
      <Stack spacing={1}>
        <Swatch label="primary.main"    hex={palette.primary.main}    fg={palette.primary.contrastText} mode={mode} />
        <Swatch label="primary.light"   hex={palette.primary.light}   fg={mode === 'dark' ? '#0E1116' : '#FFF'} mode={mode} />
        <Swatch label="primary.dark"    hex={palette.primary.dark}    fg="#FFF" mode={mode} />
        <Swatch label="secondary.main"  hex={palette.secondary.main}  fg={palette.secondary.contrastText} mode={mode} />
        <Swatch label="severity.info"    hex={palette.severity.info.main}    fg={palette.severity.info.contrastText} mode={mode} />
        <Swatch label="severity.success" hex={palette.severity.success.main} fg={palette.severity.success.contrastText} mode={mode} />
        <Swatch label="severity.warning" hex={palette.severity.warning.main} fg={palette.severity.warning.contrastText} mode={mode} />
        <Swatch label="severity.error"   hex={palette.severity.error.main}   fg={palette.severity.error.contrastText} mode={mode} />
        <Swatch label="background.default" hex={palette.background.default} fg={palette.text.primary} mode={mode} />
        <Swatch label="background.paper"   hex={palette.background.paper}   fg={palette.text.primary} mode={mode} />
        <Swatch label="text.primary"   hex={palette.text.primary === 'rgba(255,255,255,0.92)' ? '#EAECEF' : '#000000'} fg={mode==='dark'?'#0E1116':'#FFF'} mode={mode} />
        <Swatch label="divider"        hex={mode === 'dark' ? '#1A1F26' : '#E0E3E7'} fg={palette.text.secondary} mode={mode} />
      </Stack>
    </Card>
  );
}

function Swatch({ label, hex, fg, mode }) {
  return (
    <div style={{ display:'flex', alignItems:'stretch', gap: 12 }}>
      <div style={{ width: 76, height: 36, background: hex, borderRadius: 6, border:`1px solid ${mode === 'dark' ? 'rgba(255,255,255,0.1)':'rgba(0,0,0,0.1)'}`, flexShrink: 0 }} />
      <div style={{ flex: 1 }}>
        <div style={{ fontSize: '0.8125rem', fontFamily:'"JetBrains Mono",monospace', color: mode==='dark' ? 'rgba(255,255,255,0.92)' : 'rgba(0,0,0,0.87)' }}>{label}</div>
        <div style={{ fontSize: '0.75rem', fontFamily:'"JetBrains Mono",monospace', color: mode==='dark' ? 'rgba(255,255,255,0.6)' : 'rgba(0,0,0,0.6)' }}>{hex}</div>
      </div>
    </div>
  );
}

function TypeRow({ variant, label, sample }) {
  const { p } = useTheme();
  return (
    <Stack direction="row" alignItems="baseline" spacing={3}>
      <Typography variant="caption" color="secondary" sx={{ width: 360, flexShrink: 0, fontFamily:'"JetBrains Mono",monospace', fontSize:'0.6875rem' }}>{label}</Typography>
      <Typography variant={variant} sx={{ flex: 1, color: p.text.primary }}>{sample}</Typography>
    </Stack>
  );
}

function themeCodeString() {
  return `import { createTheme } from '@mui/material/styles';

declare module '@mui/material/styles' {
  interface Palette { severity: { info: PaletteColor; success: PaletteColor; warning: PaletteColor; error: PaletteColor; }; score: { s0: string; s25: string; s50: string; s75: string; s100: string; }; }
  interface TypographyVariants { code: React.CSSProperties; }
  interface TypographyVariantsOptions { code?: React.CSSProperties; }
}

export const consigliereTheme = (mode: 'light' | 'dark') => createTheme({
  palette: {
    mode,
    primary:   mode === 'dark' ? { main: '#8C9EFF', light: '#C3CEFF', dark: '#5B6BCB' } : { main: '#3D5AFE', light: '#738FFE', dark: '#0031CA' },
    secondary: mode === 'dark' ? { main: '#26DFC6' } : { main: '#00BFA5' },
    severity:  mode === 'dark'
      ? { info:{ main:'#29B6F6'}, success:{ main:'#66BB6A'}, warning:{ main:'#FFA726'}, error:{ main:'#F44336'} }
      : { info:{ main:'#0288D1'}, success:{ main:'#2E7D32'}, warning:{ main:'#ED6C02'}, error:{ main:'#D32F2F'} },
    score:     mode === 'dark'
      ? { s0:'#EF5350', s25:'#FFA726', s50:'#FFEE58', s75:'#9CCC65', s100:'#66BB6A' }
      : { s0:'#D32F2F', s25:'#F57C00', s50:'#FBC02D', s75:'#7CB342', s100:'#2E7D32' },
    background: mode === 'dark' ? { default:'#0E1116', paper:'#161B22' } : { default:'#F4F6F8', paper:'#FFFFFF' },
    divider: mode === 'dark' ? 'rgba(255,255,255,0.1)' : 'rgba(0,0,0,0.12)',
  },
  typography: {
    fontFamily: '"Roboto","Helvetica","Arial",sans-serif',
    code: { fontFamily: '"JetBrains Mono","Roboto Mono",ui-monospace,monospace', fontSize: '0.8125rem', fontWeight: 400, letterSpacing: 0 },
  },
  shape: { borderRadius: 8 },
  spacing: 8,
  components: {
    MuiDataGrid: { defaultProps: { density: 'standard' /* flip to 'compact' on dense toggle */ } },
    MuiAppBar:   { defaultProps: { color: 'transparent', elevation: 0 }, styleOverrides: { root: { backdropFilter: 'blur(8px)' } } },
    MuiCard:     { defaultProps: { variant: 'outlined' } },
    MuiButton:   { defaultProps: { disableElevation: true } },
  },
});

// Density token consumed by non-grid rows
export const rowHeight = { comfortable: 52, dense: 36 };`;
}

Object.assign(window, { TokensFrame });
