// frames-alerts.jsx — Alerts screen (hi-fi mock #4).
// Two-section vertical: active alert Cards (top) + history DataGrid (bottom).
// All four P2pAlertType rules are represented. Journal is read-only — no ack
// in this wave.

const ACTIVE_ALERTS = [
  {
    severity: 'warning',
    type: 'RelayBackRateBelowThreshold',
    fired: '14:18:02 UTC (8 min ago)',
    detail: 'Per-tick Δ relay-back ratio fell below 0.30 for 3 consecutive ticks.',
    context: [
      ['rate',        '0.21'],
      ['threshold',   '0.30'],
      ['Δrelay-back', '14'],
      ['ΔgetData',    '67'],
      ['windowMs',    '60000'],
    ],
  },
  {
    severity: 'error',
    type: 'SourceFirstDropout',
    fired: '14:11:48 UTC (14 min ago)',
    detail: 'junglebus saw zero new first-seen txs across the 60 min window while p2p + bitails kept moving.',
    context: [
      ['source',     'junglebus'],
      ['firstSeen',  '0'],
      ['otherSeen',  'p2p:4,127 · bitails:3,894'],
      ['windowMs',   '3600000'],
    ],
  },
];

const HISTORY = [
  { id:'p2p/alerts/00001735830490201', ts:'14:18:02 UTC · 8 min ago',  sev:'warning', type:'RelayBackRateBelowThreshold', detail:'rate=0.21 < 0.30 (3 consecutive ticks)' },
  { id:'p2p/alerts/00001735830115884', ts:'14:11:48 UTC · 14 min ago', sev:'error',   type:'SourceFirstDropout',           detail:'source=junglebus · firstSeen=0 · window=3600000ms' },
  { id:'p2p/alerts/00001735829641020', ts:'14:03:54 UTC · 22 min ago', sev:'warning', type:'PoolSizeBelowThreshold',       detail:'poolSize=4 < threshold=5 · recovered next tick' },
  { id:'p2p/alerts/00001735828990772', ts:'13:53:04 UTC · 32 min ago', sev:'warning', type:'PoolSizeBelowThreshold',       detail:'poolSize=4 < threshold=5' },
  { id:'p2p/alerts/00001735828114002', ts:'13:38:27 UTC · 47 min ago', sev:'warning', type:'RelayBackRateBelowThreshold', detail:'rate=0.27 < 0.30' },
  { id:'p2p/alerts/00001735822801220', ts:'12:09:54 UTC · 2h 16m ago', sev:'error',   type:'ReorgDepthExceeded',           detail:'lastDegradedReorgAt within 300000ms · depth=7' },
  { id:'p2p/alerts/00001735819007402', ts:'11:06:40 UTC · 3h 19m ago', sev:'warning', type:'SourceFirstDropout',           detail:'source=bitails · firstSeen=0 · windowMs=3600000 · recovered' },
  { id:'p2p/alerts/00001735812441004', ts:'09:17:14 UTC · 5h 08m ago', sev:'warning', type:'PoolSizeBelowThreshold',       detail:'poolSize=3 < threshold=5 · recovered after 2 ticks' },
];

function AlertsFrame({ width = 1440, height = 1000, mode = 'dark', density = 'comfortable' }) {
  const palette = ConsigliereThemeConfig.palette[mode];
  const ctx = { p: palette, t: ConsigliereThemeConfig.typography, density };
  return (
    <ThemeCtx.Provider value={ctx}>
      <div style={{ width, height, display:'flex', background: palette.background.default, color: palette.text.primary, fontFamily: 'Roboto, sans-serif', overflow:'hidden' }}>
        <ConsigliereSidebar selected="alerts" density={density} />
        <div style={{ flex:1, display:'flex', flexDirection:'column', minWidth:0 }}>
          <AppBar density={density}>
            <Toolbar sx={{ padding:'0 24px' }}>
              <Autocomplete placeholder="Search tx · address · block · token …" sx={{ width: 480, maxWidth:'40%' }} />
              <div style={{ flex:1 }} />
              <Stack direction="row" alignItems="center" spacing={1}>
                <Chip label="mainnet" size="small" color="success" variant="outlined" icon="public" />
                <Chip label="SignalR · live" size="small" variant="outlined" icon="bolt" sx={{ color: palette.severity.success.main, borderColor: palette.severity.success.main + '60' }} />
              </Stack>
              <IconButton name="notifications" badge={2} />
              <IconButton name="light_mode" />
              <IconButton name="density_medium" />
              <IconButton name="account_circle" />
            </Toolbar>
          </AppBar>

          <div style={{ flex:1, overflow:'auto', padding:24, display:'flex', flexDirection:'column', gap:24 }}>
            {/* Page header */}
            <div>
              <Typography variant="h4" sx={{ fontWeight:500, marginBottom: 4 }}>Alerts</Typography>
              <Typography variant="body2" color="secondary">
                W6 alert poller · polled every 60 s · 720-event retention · journal is read-only in this wave.
              </Typography>
            </div>

            {/* Active alerts */}
            <div>
              <Stack direction="row" alignItems="center" spacing={1.5} sx={{ marginBottom: 12 }}>
                <Typography variant="overline" color="secondary">Active alerts</Typography>
                <Chip label={`${ACTIVE_ALERTS.length} firing`} size="small" color="warning" />
              </Stack>
              <div style={{ display:'grid', gridTemplateColumns:'1fr 1fr', gap: 16 }}>
                {ACTIVE_ALERTS.map((a,i) => <ActiveAlertCard key={i} alert={a} />)}
              </div>
            </div>

            {/* History journal */}
            <div>
              <Stack direction="row" alignItems="center" justifyContent="space-between" sx={{ marginBottom: 12 }}>
                <Stack direction="row" alignItems="center" spacing={1.5}>
                  <Typography variant="overline" color="secondary">History journal</Typography>
                  <Chip label="last 24h" size="small" variant="outlined" />
                </Stack>
                <Stack direction="row" spacing={1}>
                  <Chip label="All types" size="small" variant="outlined" onDelete={() => {}} />
                  <Chip label="Last 24h" size="small" variant="outlined" onDelete={() => {}} />
                  <Button variant="outlined" size="small" startIcon={<Icon name="download" size={16} />}>Export</Button>
                </Stack>
              </Stack>
              <AlertsHistoryGrid rows={HISTORY} density={density === 'dense' ? 'compact' : 'standard'} />
            </div>
          </div>
        </div>
      </div>
    </ThemeCtx.Provider>
  );
}

function ActiveAlertCard({ alert }) {
  const { p } = useTheme();
  const sev = p.severity[alert.severity];
  return (
    <Card variant="outlined" sx={{
      borderLeft: `4px solid ${sev.main}`,
      background: p.mode==='dark' ? `linear-gradient(90deg, ${sev.main}12 0%, ${p.background.paper} 200px)` : `linear-gradient(90deg, ${sev.bg} 0%, ${p.background.paper} 240px)`,
    }}>
      <div style={{ padding: '16px 20px' }}>
        <Stack direction="row" alignItems="center" spacing={1.5} sx={{ marginBottom: 8 }}>
          <Icon name={alert.severity === 'error' ? 'error' : 'warning'} size={22} sx={{ color: sev.main }} />
          <Typography variant="h6" sx={{ fontWeight:600, fontSize:'1rem', flex:1, fontFamily:'"JetBrains Mono",monospace' }}>{alert.type}</Typography>
          <Chip label={alert.severity.toUpperCase()} size="small" color={`severity.${alert.severity}`} />
        </Stack>
        <Typography variant="body2" sx={{ marginBottom: 12, color: p.text.primary }}>{alert.detail}</Typography>
        <Stack direction="row" alignItems="center" spacing={1.5} sx={{ marginBottom: 12 }}>
          <Icon name="schedule" size={14} sx={{ color: p.text.secondary }} />
          <Typography variant="caption" color="secondary" sx={{ fontFamily:'"JetBrains Mono",monospace' }}>{alert.fired}</Typography>
        </Stack>
        <Typography variant="overline" color="secondary" sx={{ fontSize:'0.625rem', display:'block', marginBottom: 6 }}>Context</Typography>
        <div style={{ display:'grid', gridTemplateColumns:'auto 1fr', gap:'4px 16px', padding: 10, background: p.mode==='dark'?'#0B0E12':'#F7F8FA', borderRadius:6, border:`1px solid ${p.divider}` }}>
          {alert.context.map(([k,v],i) => (
            <React.Fragment key={i}>
              <Typography variant="caption" sx={{ color: p.text.secondary, fontFamily:'"JetBrains Mono",monospace' }}>{k}</Typography>
              <Typography variant="caption" sx={{ color: p.text.primary, fontFamily:'"JetBrains Mono",monospace', fontWeight: 500 }}>{v}</Typography>
            </React.Fragment>
          ))}
        </div>
      </div>
    </Card>
  );
}

function AlertsHistoryGrid({ rows, density }) {
  const { p } = useTheme();
  const sevColor = (s) => p.severity[s].main;
  const cols = [
    { field:'sev', headerName:'SEV', width: 80,
      renderCell: (r) => <Chip label={r.sev} size="small" color={`severity.${r.sev}`} sx={{ minWidth: 60, justifyContent:'center' }} /> },
    { field:'type', headerName:'Rule type', width: 280,
      renderCell: (r) => <span style={{ fontFamily:'"JetBrains Mono",monospace', fontSize:'0.8125rem', color: sevColor(r.sev) }}>{r.type}</span> },
    { field:'ts', headerName:'Fired at', width: 220,
      renderCell: (r) => <Stack direction="row" alignItems="center" spacing={0.5}><Icon name="schedule" size={14} sx={{ color: p.text.secondary }} /><span style={{ fontFamily:'"JetBrains Mono",monospace', fontSize:'0.75rem' }}>{r.ts}</span></Stack> },
    { field:'detail', headerName:'Detail', flex: 1,
      renderCell: (r) => <span style={{ fontFamily:'"JetBrains Mono",monospace', fontSize:'0.75rem', color: p.text.secondary }}>{r.detail}</span> },
    { field:'id', headerName:'Doc id', width: 280,
      renderCell: (r) => <Stack direction="row" alignItems="center" spacing={0.5}><Icon name="data_object" size={14} sx={{ color: p.text.secondary }} /><span style={{ fontFamily:'"JetBrains Mono",monospace', fontSize:'0.6875rem', color: p.text.disabled }}>{r.id}</span></Stack> },
    { field:'_', headerName:'', width: 48, sortable: false, renderCell: () => <IconButton name="open_in_new" size="small" sx={{ width:30, height:30 }} /> },
  ];
  return <DataGrid columns={cols} rows={rows} density={density} />;
}

Object.assign(window, { AlertsFrame });
