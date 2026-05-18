// frames-p2p.jsx — P2P Pool screen (hi-fi mock #5, the System section's marquee).
// Left 60-70%: DataGrid of active peers with inline score-component mini-bars.
// Right 30-40%: subnet/24 diversity donut from @mui/x-charts.

const PEERS = [
  { key:'213.108.108.219:8333',  ua:'/Bitcoin SV:1.0.16/',     ver: 70016, score: 88, latency: 142, reject: 0,  relayBack: 41, ping: 142, subnet:'213.108.108.0/24', uptime:'4h 21m', reason:'—' },
  { key:'185.114.224.41:8333',   ua:'/Bitcoin SV:1.0.16/',     ver: 70016, score: 81, latency: 198, reject: 0,  relayBack: 35, ping: 198, subnet:'185.114.224.0/24', uptime:'2h 04m', reason:'—' },
  { key:'104.131.156.18:8333',   ua:'/Bitcoin SV:1.0.15/',     ver: 70016, score: 74, latency: 244, reject: 1,  relayBack: 32, ping: 244, subnet:'104.131.156.0/24', uptime:'6h 12m', reason:'—' },
  { key:'138.68.234.91:8333',    ua:'/Bitcoin SV:1.0.16/',     ver: 70016, score: 71, latency: 287, reject: 0,  relayBack: 27, ping: 287, subnet:'138.68.234.0/24', uptime:'58m',    reason:'—' },
  { key:'45.79.180.224:8333',    ua:'/Bitcoin SV:1.0.16/',     ver: 70016, score: 65, latency: 312, reject: 2,  relayBack: 24, ping: 312, subnet:'45.79.180.0/24',  uptime:'1h 38m', reason:'—' },
  { key:'159.65.121.7:8333',     ua:'/Bitcoin SV:1.0.15/',     ver: 70016, score: 58, latency: 401, reject: 1,  relayBack: 19, ping: 401, subnet:'159.65.121.0/24', uptime:'33m',    reason:'—' },
  { key:'167.99.43.182:8333',    ua:'/Bitcoin SV:1.0.16/',     ver: 70016, score: 49, latency: 422, reject: 3,  relayBack: 17, ping: 422, subnet:'167.99.43.0/24',  uptime:'12m',    reason:'—' },
  { key:'209.97.130.4:8333',     ua:'/Bitcoin SV:1.0.14/',     ver: 70016, score: 34, latency: 488, reject: 4,  relayBack: 11, ping: 488, subnet:'209.97.130.0/24', uptime:'6m',     reason:'high RTT P95 · 1 reject this tick' },
];

// /24 diversity for donut
const SUBNETS = [
  { id:'213.108.108.0/24',  value: 1, label: 'EU · DE-FRA' },
  { id:'185.114.224.0/24',  value: 1, label: 'EU · NL-AMS' },
  { id:'104.131.156.0/24',  value: 1, label: 'NA · US-NYC' },
  { id:'138.68.234.0/24',   value: 1, label: 'NA · US-SFO' },
  { id:'45.79.180.0/24',    value: 1, label: 'NA · US-DAL' },
  { id:'159.65.121.0/24',   value: 1, label: 'APAC · SG-SIN' },
  { id:'167.99.43.0/24',    value: 1, label: 'EU · UK-LON' },
  { id:'209.97.130.0/24',   value: 1, label: 'APAC · IN-BLR' },
];

function P2pPoolFrame({ width = 1440, height = 1000, mode = 'dark', density = 'comfortable' }) {
  const palette = ConsigliereThemeConfig.palette[mode];
  const ctx = { p: palette, t: ConsigliereThemeConfig.typography, density };
  const dgDensity = density === 'dense' ? 'compact' : 'standard';
  return (
    <ThemeCtx.Provider value={ctx}>
      <div style={{ width, height, display:'flex', background: palette.background.default, color: palette.text.primary, fontFamily: 'Roboto, sans-serif', overflow:'hidden' }}>
        <ConsigliereSidebar selected="p2p" density={density} />
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

          <div style={{ flex:1, overflow:'auto', padding: 24, display:'flex', flexDirection:'column', gap: 20 }}>
            {/* Header */}
            <Stack direction="row" alignItems="flex-end" justifyContent="space-between">
              <div>
                <Stack direction="row" alignItems="center" spacing={1} sx={{ marginBottom: 4 }}>
                  <Typography variant="h4" sx={{ fontWeight:500 }}>P2P Pool</Typography>
                  <Chip label="DEV" size="small" variant="outlined" sx={{ fontSize:'0.625rem' }} />
                  <Chip label="MinScoreToRetain · 30" size="small" variant="outlined" />
                </Stack>
                <Typography variant="body2" color="secondary">
                  Active peers + per-tick scoring · rotation evicts at most one peer per tick when score &lt; 30 · scores never persisted.
                </Typography>
              </div>
              <Stack direction="row" spacing={1}>
                <Button variant="outlined" size="small" startIcon={<Icon name="refresh" size={16} />}>Refresh</Button>
                <Button variant="outlined" size="small" startIcon={<Icon name="download" size={16} />}>Export CSV</Button>
              </Stack>
            </Stack>

            {/* KPI strip */}
            <div style={{ display:'grid', gridTemplateColumns:'repeat(4, 1fr)', gap: 16 }}>
              <KpiCard label="Active peers"          value="8 / 8"  caption="target 8 · 0 evictions this hour" color={palette.severity.success.main} icon="hub" />
              <KpiCard label="Subnet/24 diversity"   value="8"      caption="all unique · no concentration risk" color={palette.severity.success.main} icon="diversity_3" />
              <KpiCard label="Worst-peer score"      value="34"     caption="floor 30 · within tolerance"       color={palette.severity.warning.main} icon="speed" />
              <KpiCard label="Inbound listener"      value="OFF"    caption="config-only stub · W6 baseline"     color={palette.text.disabled} icon="settings_input_antenna" />
            </div>

            {/* Main split */}
            <div style={{ display:'grid', gridTemplateColumns: '2fr 1fr', gap: 20, alignItems:'stretch' }}>
              <Card variant="outlined">
                <CardHeader
                  title="Active peers"
                  subheader="GET /api/admin/p2p/peers · live · sort by score asc to expose rotation candidates"
                  action={
                    <Stack direction="row" spacing={1}>
                      <Chip label="UA: 3 versions" size="small" variant="outlined" />
                      <Chip label="Sorted: score ↑" size="small" variant="outlined" color="primary" />
                    </Stack>
                  }
                />
                <PeersGrid rows={PEERS} density={dgDensity} />
              </Card>

              <Card variant="outlined" sx={{ display:'flex', flexDirection:'column' }}>
                <CardHeader
                  title="Subnet /24 diversity"
                  subheader="One peer per /24 · sparse cluster = healthy"
                  action={<Chip label="8 / 8" size="small" color="success" variant="outlined" />}
                />
                <Divider />
                <div style={{ flex:1, padding: 20, display:'flex', flexDirection:'column', alignItems:'center', gap: 16 }}>
                  <div style={{ position:'relative', width: 220, height: 220 }}>
                    <Donut data={SUBNETS} size={220} innerRadius={0.62} />
                    <div style={{ position:'absolute', inset:0, display:'flex', flexDirection:'column', alignItems:'center', justifyContent:'center' }}>
                      <Typography variant="h3" sx={{ fontWeight:500, fontSize:'2rem' }}>8</Typography>
                      <Typography variant="caption" color="secondary">unique /24</Typography>
                    </div>
                  </div>
                  <Stack spacing={0.5} sx={{ width:'100%' }}>
                    {SUBNETS.map((s, i) => <SubnetLegendItem key={i} subnet={s} idx={i} />)}
                  </Stack>
                </div>
              </Card>
            </div>
          </div>
        </div>
      </div>
    </ThemeCtx.Provider>
  );
}

function KpiCard({ label, value, caption, color, icon }) {
  const { p } = useTheme();
  return (
    <Card variant="outlined" sx={{ padding: '16px 20px' }}>
      <Stack direction="row" alignItems="flex-start" spacing={1.5}>
        <div style={{ width: 36, height: 36, borderRadius: 8, background: color + '22', display:'flex', alignItems:'center', justifyContent:'center', flexShrink:0 }}>
          <Icon name={icon} size={20} sx={{ color }} />
        </div>
        <div style={{ flex: 1, minWidth: 0 }}>
          <Typography variant="overline" color="secondary" sx={{ lineHeight:1.2, display:'block' }}>{label}</Typography>
          <Typography variant="h3" sx={{ fontFamily:'"JetBrains Mono",monospace', fontWeight:500, fontSize:'1.625rem', lineHeight:1.2, marginTop: 4 }}>{value}</Typography>
          <Typography variant="caption" color="secondary" sx={{ fontSize:'0.6875rem' }}>{caption}</Typography>
        </div>
      </Stack>
    </Card>
  );
}

function PeersGrid({ rows, density }) {
  const { p } = useTheme();
  const cols = [
    { field:'key', headerName:'Peer key', flex: 1.6,
      renderCell: (r) => <Stack direction="row" alignItems="center" spacing={1} sx={{ width:'100%' }}>
        <span style={{ width:6, height:6, borderRadius:'50%', background: p.severity.success.main, flexShrink:0 }} />
        <span style={{ fontFamily:'"JetBrains Mono",monospace', fontSize:'0.8125rem', overflow:'hidden', textOverflow:'ellipsis' }}>{r.key}</span>
      </Stack> },
    { field:'score', headerName:'Score', width: 140,
      renderCell: (r) => <ScoreBar score={r.score} /> },
    { field:'components', headerName:'Latency · Reject · RelayBack', flex: 1.4,
      renderCell: (r) => (
        <Stack direction="row" spacing={1.5} alignItems="center" sx={{ width:'100%' }}>
          <Stack alignItems="flex-start" spacing={0.5}>
            <MiniBar value={Math.min(60, r.latency/10)} max={60} color={p.severity.warning.main} width={48} />
            <Typography variant="caption" sx={{ fontSize:'0.625rem', color: p.text.secondary, lineHeight:1 }}>lat −{Math.min(60, Math.round(r.latency/10))}</Typography>
          </Stack>
          <Stack alignItems="flex-start" spacing={0.5}>
            <MiniBar value={Math.min(50, r.reject*5)} max={50} color={p.severity.error.main} width={36} />
            <Typography variant="caption" sx={{ fontSize:'0.625rem', color: p.text.secondary, lineHeight:1 }}>rej −{Math.min(50, r.reject*5)}</Typography>
          </Stack>
          <Stack alignItems="flex-start" spacing={0.5}>
            <MiniBar value={Math.min(50, r.relayBack)} max={50} color={p.severity.success.main} width={48} />
            <Typography variant="caption" sx={{ fontSize:'0.625rem', color: p.text.secondary, lineHeight:1 }}>+{Math.min(50, r.relayBack)}</Typography>
          </Stack>
        </Stack>
      ) },
    { field:'ping', headerName:'Ping P95', width: 100,
      renderCell: (r) => <span style={{ fontFamily:'"JetBrains Mono",monospace', fontSize:'0.8125rem', color: r.ping > 400 ? p.severity.warning.main : p.text.primary }}>{r.ping} ms</span> },
    { field:'relayBack', headerName:'RelayBack', width: 100,
      renderCell: (r) => <Stack direction="row" alignItems="center" spacing={0.5}><Icon name="repeat" size={14} sx={{ color: p.text.secondary }} /><span style={{ fontFamily:'"JetBrains Mono",monospace', fontSize:'0.8125rem' }}>{r.relayBack}</span></Stack> },
    { field:'reject', headerName:'Reject', width: 80,
      renderCell: (r) => <span style={{ fontFamily:'"JetBrains Mono",monospace', fontSize:'0.8125rem', color: r.reject > 0 ? p.severity.error.main : p.text.secondary }}>{r.reject}</span> },
    { field:'uptime', headerName:'Uptime', width: 100,
      renderCell: (r) => <span style={{ fontFamily:'"JetBrains Mono",monospace', fontSize:'0.75rem', color: p.text.secondary }}>{r.uptime}</span> },
  ];
  return <DataGrid columns={cols} rows={rows} density={density} sx={{ borderRadius: 0, border: 'none' }} />;
}

function SubnetLegendItem({ subnet, idx }) {
  const { p } = useTheme();
  const palette = [p.primary.main, p.secondary.main, p.severity.warning.main, p.severity.success.main, p.severity.info.main, p.primary.light, p.secondary.light, p.severity.error.main];
  const c = palette[idx % palette.length];
  return (
    <Stack direction="row" alignItems="center" spacing={1} sx={{ padding:'4px 0' }}>
      <span style={{ width:10, height:10, borderRadius:2, background:c, flexShrink:0 }} />
      <Typography variant="caption" sx={{ flex:1, fontFamily:'"JetBrains Mono",monospace', fontSize:'0.6875rem' }}>{subnet.id}</Typography>
      <Typography variant="caption" color="secondary" sx={{ fontSize:'0.625rem' }}>{subnet.label}</Typography>
    </Stack>
  );
}

Object.assign(window, { P2pPoolFrame });
