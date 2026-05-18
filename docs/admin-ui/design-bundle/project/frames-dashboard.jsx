// frames-dashboard.jsx — Dashboard frame, with variants for the 4 required modes.
// Composition per brief §5.1:
//   Hero (big system-health + mempool sparkline) → Search → Activity stream + Sparkline panel.

// ── Realistic data ──────────────────────────────────────────────────────────
const SPARK_MEMPOOL = [3.2,3.4,3.1,3.6,3.8,3.5,3.9,4.1,4.0,3.7,3.9,4.2,4.4,4.1,4.3,4.5,4.2,4.6,4.8,4.5,4.7,5.0,4.8,4.9,5.1];
const SPARK_POOL    = [8,8,7,8,8,8,8,7,8,8,7,8,8,8,8,8,7,8,8,8,8,8,8,8,8];
const SPARK_HEALTH  = [98,97,98,98,99,98,97,98,99,99,98,98,99,99,98,99,99,99,98,99,99,99,99,99,99];

const RECENT_LOOKUPS = [
  { icon:'swap_horiz', label:'a91f…7c4d' },
  { icon:'account_balance_wallet', label:'1Q2T…hWj9' },
  { icon:'token', label:'DSTAS:cafe…0fbe' },
  { icon:'link', label:'block 856,221' },
  { icon:'swap_horiz', label:'7b3a…22ee' },
];

const RECENT_BROADCASTS = [
  { txid:'a91f3c12d8…7c4d',  state:'Mined',       sev:'success', age:'12s', source:'p2p' },
  { txid:'7b3a8801ff…22ee',  state:'PeerRelayed', sev:'info',    age:'48s', source:'p2p' },
  { txid:'04b29fa6c1…91a0',  state:'Dispatching', sev:'warning', age:'1m 4s', source:'p2p' },
  { txid:'e2da1bf701…3a8c',  state:'Validated',   sev:'info',    age:'2m 11s', source:'—' },
  { txid:'1cc890af4b…44de',  state:'Mined',       sev:'success', age:'2m 38s', source:'p2p' },
  { txid:'9f01a2dd0b…71fa',  state:'Mined',       sev:'success', age:'3m 02s', source:'p2p' },
];

const SOURCE_VISIBILITY = [
  { id:'p2p',       icon:'hub',          name:'P2P',       rate:1.21, peers:8, ok:true },
  { id:'bitails',   icon:'public',       name:'Bitails',   rate:1.14, peers:'REST', ok:true },
  { id:'junglebus', icon:'travel_explore', name:'JungleBus',rate:0.31, peers:'REST', ok:true },
];

// ── DashboardFrame — composable for the 4 variants ──────────────────────────
function DashboardFrame({ mode = 'dark', density = 'comfortable', viewport = 'desktop', width = 1440, height = 900 }) {
  const palette = ConsigliereThemeConfig.palette[mode];
  const ctx = { p: palette, t: ConsigliereThemeConfig.typography, density };
  const sidebarW = 240;
  const appH = viewport === 'mobile' ? 56 : (density === 'dense' ? 48 : 64);
  const dense = density === 'dense';

  return (
    <ThemeCtx.Provider value={ctx}>
      <div style={{ width, height, display:'flex', background: palette.background.default, color: palette.text.primary, fontFamily: 'Roboto, sans-serif', overflow:'hidden' }}>
        {viewport === 'desktop' && <ConsigliereSidebar selected="dashboard" density={density} />}

        <div style={{ flex:1, display:'flex', flexDirection:'column', minWidth:0 }}>
          {/* AppBar */}
          <AppBar density={density} height={viewport === 'mobile' ? 56 : 64}>
            <Toolbar sx={{ padding: viewport === 'mobile' ? '0 8px' : '0 24px' }}>
              {viewport === 'mobile' && <IconButton name="menu" edge="start" />}
              {viewport === 'mobile' && (
                <Stack direction="row" alignItems="center" spacing={1} sx={{ flex:1, marginLeft:4 }}>
                  <div style={{ width:24, height:24, borderRadius:6, background:`linear-gradient(135deg, ${palette.primary.main}, ${palette.secondary.main})`, display:'flex', alignItems:'center', justifyContent:'center' }}>
                    <Icon name="memory" size={14} sx={{ color:'#FFF' }} />
                  </div>
                  <Typography variant="h6" sx={{ fontSize:'0.9375rem', fontWeight:600 }}>Consigliere</Typography>
                </Stack>
              )}
              {viewport !== 'mobile' && (
                <Autocomplete placeholder="Search tx · address · block · token …" sx={{ width: 480, maxWidth:'40%' }} />
              )}
              <div style={{ flex:1 }} />
              {viewport !== 'mobile' && (
                <Stack direction="row" alignItems="center" spacing={1}>
                  <Chip label="mainnet" size="small" color="success" variant="outlined" icon="public" />
                  <Chip label="SignalR · live" size="small" variant="outlined" icon="bolt" sx={{ color: palette.severity.success.main, borderColor: palette.severity.success.main + '60' }} />
                </Stack>
              )}
              <IconButton name="notifications" badge={3} />
              {viewport !== 'mobile' && <IconButton name={mode === 'dark' ? 'light_mode' : 'dark_mode'} />}
              {viewport !== 'mobile' && <IconButton name="density_medium" />}
              <IconButton name="account_circle" />
            </Toolbar>
          </AppBar>

          {/* Body */}
          <div style={{ flex:1, overflow:'auto', padding: viewport === 'mobile' ? 16 : (dense ? 16 : 24), display:'flex', flexDirection:'column', gap: dense ? 16 : 24 }}>
            {/* HERO + Sparkline panel — composite system health */}
            <DashboardHero mode={mode} density={density} viewport={viewport} />

            {/* Search prompt (double-entry, per brief) */}
            {viewport !== 'mobile' && (
              <Card variant="outlined" sx={{ padding: dense ? '16px 24px' : '24px 32px' }}>
                <Stack spacing={2} alignItems="center">
                  <Typography variant="overline" color="secondary">Look up an entity</Typography>
                  <Autocomplete placeholder="txid · address · block height · token-id" size="medium" variant="filled" sx={{ width: '100%', maxWidth: 720, height: 48 }} />
                  <Stack direction="row" spacing={1} wrap alignItems="center" justifyContent="center" sx={{ marginTop: 4 }}>
                    <Typography variant="caption" color="secondary" sx={{ marginRight:4 }}>RECENT</Typography>
                    {RECENT_LOOKUPS.map((r,i) => (
                      <Chip key={i} icon={r.icon} label={<span style={{ fontFamily:'"JetBrains Mono",monospace', fontSize:'0.75rem' }}>{r.label}</span>} variant="outlined" size="small" />
                    ))}
                  </Stack>
                </Stack>
              </Card>
            )}

            {/* Activity stream + Source visibility */}
            <div style={{ display:'grid', gridTemplateColumns: viewport === 'mobile' ? '1fr' : '2fr 1fr', gap: dense ? 16 : 24 }}>
              {/* Recent broadcasts */}
              <Card variant="outlined">
                <CardHeader
                  title="Recent broadcasts"
                  subheader="Our outgoing tx · newest first"
                  action={<Stack direction="row" spacing={1} alignItems="center"><Chip label={`${RECENT_BROADCASTS.length} in window`} size="small" variant="outlined" /><IconButton name="more_horiz" size="small" /></Stack>}
                />
                <Divider />
                <List dense={dense} sx={{ padding: 0 }}>
                  {RECENT_BROADCASTS.slice(0, viewport === 'mobile' ? 4 : 6).map((b,i) => (
                    <li key={i} style={{ display:'flex', alignItems:'center', gap:12, padding: dense ? '8px 16px' : '12px 16px', borderBottom: i < RECENT_BROADCASTS.length-1 ? `1px solid ${palette.divider}` : 'none' }}>
                      <Icon name="swap_horiz" size={18} sx={{ color: palette.text.secondary }} />
                      <span style={{ flex:1, fontFamily:'"JetBrains Mono",monospace', fontSize:'0.8125rem', color: palette.text.primary, whiteSpace:'nowrap', overflow:'hidden', textOverflow:'ellipsis' }}>{b.txid}</span>
                      <Chip label={b.source} variant="outlined" size="small" sx={{ height:18, fontSize:'0.625rem', textTransform:'lowercase' }} />
                      <Chip label={b.state} size="small" color={`severity.${b.sev}`} sx={{ minWidth: 90, justifyContent:'center' }} />
                      <Typography variant="caption" color="secondary" sx={{ fontFamily:'"JetBrains Mono",monospace', minWidth: 50, textAlign:'right' }}>{b.age}</Typography>
                    </li>
                  ))}
                </List>
              </Card>

              {/* Source visibility */}
              <Card variant="outlined">
                <CardHeader
                  title="Per-source visibility"
                  subheader="Live first-seen rate · 60 s window"
                  action={<Chip label="live" size="small" color="success" variant="outlined" icon="circle" />}
                />
                <Divider />
                <Stack spacing={0} sx={{ padding: '4px 0' }}>
                  {SOURCE_VISIBILITY.map((s,i) => (
                    <div key={s.id} style={{ padding: dense ? '10px 16px' : '14px 16px', borderBottom: i < SOURCE_VISIBILITY.length-1 ? `1px solid ${palette.divider}` : 'none' }}>
                      <Stack direction="row" alignItems="center" spacing={1.5}>
                        <Icon name={s.icon} size={18} sx={{ color: palette.text.secondary }} />
                        <div style={{ flex:1 }}>
                          <Typography variant="subtitle2" sx={{ fontWeight: 600 }}>{s.name}</Typography>
                          <Typography variant="caption" color="secondary">{typeof s.peers === 'number' ? `${s.peers} peers` : `${s.peers} ingress`}</Typography>
                        </div>
                        <div style={{ textAlign:'right' }}>
                          <Typography variant="h6" sx={{ fontFamily:'"JetBrains Mono",monospace', fontWeight:600, fontSize:'1.125rem', color: palette.severity.success.main }}>{s.rate.toFixed(2)}</Typography>
                          <Typography variant="caption" color="secondary">tx/s</Typography>
                        </div>
                      </Stack>
                    </div>
                  ))}
                </Stack>
              </Card>
            </div>
          </div>
        </div>
      </div>
    </ThemeCtx.Provider>
  );
}

// ── Hero — the across-the-room signal ───────────────────────────────────────
function DashboardHero({ mode, density, viewport }) {
  const { p } = useTheme();
  const dense = density === 'dense';
  return (
    <div style={{ display:'grid', gridTemplateColumns: viewport === 'mobile' ? '1fr' : '1.4fr 1fr 1fr', gap: dense ? 16 : 24 }}>
      {/* System health composite */}
      <Card variant="outlined" sx={{ padding: dense ? 20 : 28, background: mode === 'dark' ? p.background.paper : '#FFF', overflow:'hidden', position:'relative' }}>
        <div style={{ position:'absolute', top:0, right:0, width:160, height:'100%', background: `radial-gradient(circle at 100% 0%, ${p.severity.success.main}22, transparent 60%)`, pointerEvents:'none' }} />
        <Stack direction="row" alignItems="center" spacing={2} sx={{ marginBottom: 8 }}>
          <div style={{ position:'relative' }}>
            <div style={{
              width: 14, height: 14, borderRadius:'50%', background: p.severity.success.main,
              boxShadow: `0 0 0 6px ${p.severity.success.main}33, 0 0 0 12px ${p.severity.success.main}1a`,
            }} />
          </div>
          <Typography variant="overline" color="secondary">System health</Typography>
        </Stack>
        <Typography variant={dense ? 'h3' : 'h2'} sx={{ fontWeight: 300, fontSize: viewport === 'mobile' ? '2rem' : (dense ? '2.25rem' : '3rem'), marginBottom: 4, letterSpacing:'-0.5px' }}>
          All systems nominal
        </Typography>
        <Typography variant="body2" color="secondary" sx={{ marginBottom: dense ? 12 : 20 }}>
          Pool 8/8 · headers 856,221 · broadcast pipeline idle · 0 alerts firing
        </Typography>
        <Stack direction="row" spacing={dense ? 1.5 : 3} alignItems="center" wrap>
          <SubMetric icon="hub"          label="P2P Pool"   value="8 / 8"      sub="subnet/24 div 6" color={p.severity.success.main} />
          <SubMetric icon="link"         label="Headers"    value="856,221"    sub="tip 14:23:11 UTC" color={p.severity.success.main} />
          <SubMetric icon="cell_tower"   label="Broadcast"  value="3 in flight" sub="0 stuck > 5m"     color={p.severity.success.main} />
          <SubMetric icon="notifications" label="Alerts"    value="0 firing"   sub="3 in last 24h"   color={p.severity.success.main} />
        </Stack>
      </Card>

      {/* Mempool rate sparkline */}
      <Card variant="outlined" sx={{ padding: dense ? 16 : 20 }}>
        <Stack direction="row" alignItems="flex-start" justifyContent="space-between">
          <div>
            <Typography variant="overline" color="secondary">Mempool tx-rate</Typography>
            <Typography variant="h3" sx={{ fontFamily:'"JetBrains Mono",monospace', fontWeight:500, fontSize: dense ? '1.75rem' : '2.25rem', marginTop: 2, color: p.text.primary }}>5.1 <span style={{ fontSize:'0.875rem', color: p.text.secondary }}>tx/s</span></Typography>
            <Stack direction="row" alignItems="center" spacing={0.5} sx={{ marginTop: 2 }}>
              <Icon name="trending_up" size={14} sx={{ color: p.severity.success.main }} />
              <Typography variant="caption" sx={{ color: p.severity.success.main }}>+12.4% last 5 min</Typography>
            </Stack>
          </div>
          <Chip label="60s window" size="small" variant="outlined" sx={{ marginTop: 4 }} />
        </Stack>
        <div style={{ marginTop: dense ? 12 : 16, marginLeft:-8, marginRight:-8 }}>
          <Sparkline data={SPARK_MEMPOOL} width={viewport === 'mobile' ? 300 : 280} height={dense ? 56 : 72} color={p.primary.main} showDots />
        </div>
      </Card>

      {/* Pool size sparkline */}
      <Card variant="outlined" sx={{ padding: dense ? 16 : 20 }}>
        <Stack direction="row" alignItems="flex-start" justifyContent="space-between">
          <div>
            <Typography variant="overline" color="secondary">Active peer pool</Typography>
            <Typography variant="h3" sx={{ fontFamily:'"JetBrains Mono",monospace', fontWeight:500, fontSize: dense ? '1.75rem' : '2.25rem', marginTop: 2 }}>8 <span style={{ fontSize:'0.875rem', color: p.text.secondary }}>/ 8</span></Typography>
            <Stack direction="row" alignItems="center" spacing={0.5} sx={{ marginTop: 2 }}>
              <span style={{ width:8, height:8, borderRadius:'50%', background: p.severity.success.main }} />
              <Typography variant="caption" color="secondary">stable · last evict 38m ago</Typography>
            </Stack>
          </div>
          <Chip label="target 8" size="small" variant="outlined" sx={{ marginTop: 4 }} />
        </Stack>
        <div style={{ marginTop: dense ? 12 : 16, marginLeft:-8, marginRight:-8 }}>
          <Sparkline data={SPARK_POOL} width={viewport === 'mobile' ? 300 : 280} height={dense ? 56 : 72} color={p.secondary.main} showDots />
        </div>
      </Card>
    </div>
  );
}

function SubMetric({ icon, label, value, sub, color }) {
  const { p } = useTheme();
  return (
    <Stack direction="row" alignItems="center" spacing={1.5} sx={{ flex:1, minWidth:0 }}>
      <div style={{ width:36, height:36, borderRadius:8, background: color + '22', display:'flex', alignItems:'center', justifyContent:'center', flexShrink:0 }}>
        <Icon name={icon} size={20} sx={{ color }} />
      </div>
      <div style={{ minWidth:0 }}>
        <Typography variant="caption" color="secondary" sx={{ display:'block', lineHeight:1.2 }}>{label}</Typography>
        <Typography variant="subtitle2" sx={{ fontWeight:600, fontSize:'0.875rem' }} noWrap>{value}</Typography>
        <Typography variant="caption" color="secondary" sx={{ fontSize:'0.6875rem', lineHeight:1.2 }} noWrap>{sub}</Typography>
      </div>
    </Stack>
  );
}

Object.assign(window, { DashboardFrame, SPARK_MEMPOOL, SPARK_POOL });
