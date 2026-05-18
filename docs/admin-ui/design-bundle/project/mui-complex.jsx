// mui-complex.jsx — heavier MUI primitives: AppBar/Drawer shell, DataGrid mock,
// vertical Stepper (for the Tx timeline), and @mui/x-charts mocks (Sparkline + Donut).

// ─── Autocomplete (freeSolo) — smart search ─────────────────────────────────
function Autocomplete({ placeholder = 'Search', value = '', size = 'small', sx = {}, recent = [], variant = 'standard', startAdornment }) {
  const { p } = useTheme();
  const h = size === 'small' ? 36 : 40;
  return (
    <div style={{
      display:'flex', alignItems:'center', height:h,
      background: variant === 'filled' ? (p.mode==='dark' ? 'rgba(255,255,255,0.06)' : 'rgba(0,0,0,0.04)') : p.background.paper,
      border: variant === 'filled' ? `1px solid transparent` : `1px solid ${p.divider}`,
      borderRadius: 8, padding:'0 12px', gap:8, ...sx,
    }}>
      {startAdornment || <Icon name="search" size={18} sx={{ color: p.text.secondary }} />}
      <span style={{ flex:1, color: value ? p.text.primary : p.text.secondary, fontSize:'0.875rem', fontFamily:'inherit', whiteSpace:'nowrap', overflow:'hidden', textOverflow:'ellipsis' }}>
        {value || placeholder}
      </span>
      {value && <Icon name="close" size={16} sx={{ color: p.text.secondary, cursor:'pointer' }} />}
      <Chip label="⌘K" size="small" variant="outlined" sx={{ height:18, fontSize:'0.625rem' }} />
    </div>
  );
}

// ─── AppBar + Toolbar ───────────────────────────────────────────────────────
function AppBar({ children, sx = {}, position = 'static', height = 64, density = 'comfortable' }) {
  const { p } = useTheme();
  const h = density === 'dense' ? 48 : height;
  return (
    <header style={{
      height: h, background: p.background.paper, borderBottom:`1px solid ${p.divider}`,
      display:'flex', alignItems:'center', flexShrink:0,
      position: position === 'sticky' ? 'sticky' : 'relative', top:0, zIndex: 1100,
      backdropFilter:'blur(8px)', ...sx,
    }}>{children}</header>
  );
}
function Toolbar({ children, sx = {} }) {
  return <div style={{ display:'flex', alignItems:'center', gap:8, padding:'0 16px', width:'100%', ...sx }}>{children}</div>;
}

// ─── Drawer (permanent variant — desktop sidebar) ───────────────────────────
function Drawer({ children, width = 240, sx = {} }) {
  const { p } = useTheme();
  return (
    <aside style={{
      width, flexShrink:0, background: p.background.paper, borderRight:`1px solid ${p.divider}`,
      display:'flex', flexDirection:'column', overflow:'hidden', ...sx,
    }}>{children}</aside>
  );
}

// ─── Sidebar — composed Drawer + List for Consigliere IA ────────────────────
const SIDEBAR_OPS = [
  { id:'dashboard',  label:'Dashboard',       icon:'dashboard' },
  { id:'tx',         label:'Transactions',    icon:'swap_horiz' },
  { id:'queue',      label:'Broadcast Queue', icon:'cell_tower' },
  { id:'addresses',  label:'Addresses',       icon:'account_balance_wallet' },
  { id:'tokens',     label:'Tokens',          icon:'token' },
  { id:'alerts',     label:'Alerts',          icon:'notifications', badge: 3 },
];
const SIDEBAR_SYS = [
  { id:'p2p',        label:'P2P Pool',           icon:'hub' },
  { id:'sources',    label:'Source Metrics',     icon:'insights' },
  { id:'headers',    label:'Headers Chain',      icon:'link' },
  { id:'inspector',  label:'Broadcast Inspector',icon:'play_circle' },
  { id:'config',     label:'Configuration',      icon:'tune' },
  { id:'logs',       label:'Logs / Raw',         icon:'description' },
  { id:'providers',  label:'Providers',          icon:'lan' },
  { id:'setup',      label:'Setup',              icon:'rocket_launch' },
];

function ConsigliereSidebar({ selected = 'dashboard', density = 'comfortable', width = 240 }) {
  const { p } = useTheme();
  const dense = density === 'dense';
  return (
    <Drawer width={width}>
      {/* Brand */}
      <div style={{ display:'flex', alignItems:'center', gap:10, height: dense ? 48 : 64, padding:'0 16px', borderBottom:`1px solid ${p.divider}`, flexShrink:0 }}>
        <div style={{
          width: 28, height: 28, borderRadius: 8,
          background: `linear-gradient(135deg, ${p.primary.main}, ${p.secondary.main})`,
          display:'flex', alignItems:'center', justifyContent:'center', flexShrink:0,
        }}>
          <Icon name="memory" size={18} sx={{ color: '#FFF' }} />
        </div>
        <div style={{ minWidth:0 }}>
          <Typography variant="h6" sx={{ fontSize:'0.9375rem', fontWeight:600, lineHeight:1.2 }}>Consigliere</Typography>
          <Typography variant="caption" color="secondary" sx={{ fontSize:'0.6875rem' }}>BSV thin-node observer</Typography>
        </div>
      </div>

      {/* Scroll */}
      <div style={{ flex:1, overflow:'auto', padding: dense ? '8px 0' : '12px 0' }}>
        <Typography variant="overline" color="secondary" sx={{ padding:'0 24px', fontSize:'0.6875rem', display:'block', marginBottom:4 }}>Operator</Typography>
        <List dense={dense}>
          {SIDEBAR_OPS.map(item => (
            <ListItemButton key={item.id} selected={selected === item.id} dense={dense}>
              <Icon name={item.icon} size={dense ? 18 : 20} />
              <span style={{ flex:1, fontSize: dense ? '0.8125rem' : '0.875rem' }}>{item.label}</span>
              {item.badge && <Chip label={item.badge} size="small" color="severity.error" sx={{ height:18, minWidth:18, padding:'0 6px' }} />}
            </ListItemButton>
          ))}
        </List>
        <Divider sx={{ margin:'12px 16px' }}>System</Divider>
        <List dense={dense}>
          {SIDEBAR_SYS.map(item => (
            <ListItemButton key={item.id} selected={selected === item.id} dense={dense}>
              <Icon name={item.icon} size={dense ? 18 : 20} />
              <span style={{ flex:1, fontSize: dense ? '0.8125rem' : '0.875rem' }}>{item.label}</span>
              <Chip label="DEV" size="small" variant="outlined" sx={{ height:16, fontSize:'0.5625rem', padding:'0 5px', color: p.text.secondary, borderColor: p.divider }} />
            </ListItemButton>
          ))}
        </List>
      </div>

      {/* Footer — env tag + theme/density toggles */}
      <div style={{ padding:'12px 16px', borderTop:`1px solid ${p.divider}`, display:'flex', alignItems:'center', gap:8, flexShrink:0 }}>
        <Stack direction="row" spacing={1} alignItems="center" sx={{ flex:1, minWidth:0 }}>
          <span style={{ width:8, height:8, borderRadius:'50%', background: p.severity.success.main, flexShrink:0 }} />
          <Typography variant="caption" sx={{ fontSize:'0.75rem' }} color="secondary" noWrap>node-prd-eu-01</Typography>
        </Stack>
        <IconButton name={p.mode === 'dark' ? 'light_mode' : 'dark_mode'} size="small" />
        <IconButton name="density_medium" size="small" />
      </div>
    </Drawer>
  );
}

// ─── DataGrid mock ──────────────────────────────────────────────────────────
// Real MUI DataGrid sizing: row 36 compact / 52 standard; header 56; column dividers 1px.
function DataGrid({ columns, rows, density = 'standard', sx = {}, hideHeader = false, headerHeight }) {
  const { p } = useTheme();
  const rowH = density === 'compact' ? 36 : 52;
  const headH = headerHeight ?? (density === 'compact' ? 40 : 56);
  const totalGrow = columns.reduce((s,c) => s + (c.flex || 0), 0);
  const sized = columns.map(c => ({ ...c, _w: c.width || (c.flex ? `${(c.flex/totalGrow)*100}%` : 120) }));
  return (
    <div style={{ background: p.background.paper, border:`1px solid ${p.divider}`, borderRadius:8, overflow:'hidden', ...sx }}>
      {!hideHeader && (
        <div style={{ display:'flex', height: headH, borderBottom:`1px solid ${p.divider}`, background: p.mode==='dark' ? p.background.elev1 : p.background.elev2, alignItems:'center' }}>
          {sized.map((c, i) => (
            <div key={c.field} style={{
              flex: typeof c._w === 'string' ? `1 1 ${c._w}` : `0 0 ${c._w}px`,
              padding:'0 12px', display:'flex', alignItems:'center', gap:4,
              borderRight: i < sized.length-1 ? `1px solid ${p.divider}` : 'none',
              fontSize:'0.75rem', fontWeight:600, color: p.text.primary,
              letterSpacing:'0.4px', textTransform:'uppercase',
            }}>
              {c.headerName}
              {c.sortable !== false && <Icon name="arrow_drop_down" size={16} sx={{ color: p.text.secondary, opacity:0.4 }} />}
            </div>
          ))}
        </div>
      )}
      {rows.map((row, ri) => (
        <div key={ri} style={{
          display:'flex', height: rowH, borderBottom: ri < rows.length-1 ? `1px solid ${p.divider}` : 'none',
          background: row._sel ? p.action.selected : (ri % 2 && p.mode==='dark' ? 'rgba(255,255,255,0.015)' : 'transparent'),
          alignItems:'center',
        }}>
          {sized.map((c, i) => (
            <div key={c.field} style={{
              flex: typeof c._w === 'string' ? `1 1 ${c._w}` : `0 0 ${c._w}px`,
              padding:'0 12px', display:'flex', alignItems:'center', gap:6,
              borderRight: i < sized.length-1 ? `1px solid ${p.divider}` : 'none',
              fontSize: density === 'compact' ? '0.8125rem' : '0.875rem',
              minWidth: 0, overflow:'hidden',
            }}>
              {c.renderCell ? c.renderCell(row) : (
                <span style={{ whiteSpace:'nowrap', overflow:'hidden', textOverflow:'ellipsis', color: p.text.primary }}>
                  {row[c.field]}
                </span>
              )}
            </div>
          ))}
        </div>
      ))}
    </div>
  );
}

// ─── Stepper (vertical) ─────────────────────────────────────────────────────
// Each step = { label, sublabel, timestamp, source, error, state: 'done'|'active'|'pending'|'error' }
function Stepper({ steps, sx = {} }) {
  const { p } = useTheme();
  const colorFor = (s) =>
    s === 'done' ? p.severity.success.main :
    s === 'active' ? p.primary.main :
    s === 'error' ? p.severity.error.main :
    p.text.disabled;
  return (
    <div style={{ ...sx }}>
      {steps.map((step, i) => {
        const c = colorFor(step.state);
        const last = i === steps.length - 1;
        return (
          <div key={i} style={{ display:'flex', gap:16, alignItems:'flex-start' }}>
            {/* Icon column */}
            <div style={{ display:'flex', flexDirection:'column', alignItems:'center', flexShrink:0 }}>
              <div style={{
                width: step.state === 'active' ? 32 : 28, height: step.state === 'active' ? 32 : 28,
                borderRadius:'50%', background: step.state === 'pending' ? 'transparent' : c,
                border: step.state === 'pending' ? `2px solid ${p.divider}` : `2px solid ${c}`,
                color: '#FFF', display:'flex', alignItems:'center', justifyContent:'center',
                fontSize: '0.75rem', fontWeight: 600,
                boxShadow: step.state === 'active' ? `0 0 0 4px ${c}33` : 'none',
                transition: 'all 200ms', flexShrink:0,
              }}>
                {step.state === 'done' ? <Icon name="check" size={16} sx={{ color:'#FFF' }} />
                  : step.state === 'error' ? <Icon name="close" size={16} sx={{ color:'#FFF' }} />
                  : step.state === 'active' ? <Icon name="autorenew" size={16} sx={{ color:'#FFF' }} />
                  : <Typography variant="caption" sx={{ color: p.text.disabled, fontSize:'0.75rem', fontWeight:600 }}>{i+1}</Typography>}
              </div>
              {!last && (
                <div style={{
                  width:2, flex:1, minHeight: 56,
                  background: step.state === 'done' ? p.severity.success.main : p.divider,
                  marginTop: 2,
                }} />
              )}
            </div>
            {/* Content column */}
            <div style={{ paddingBottom: 28, flex:1, minWidth:0 }}>
              <Stack direction="row" alignItems="center" spacing={1} sx={{ marginBottom: 4 }}>
                <Typography variant="subtitle1" sx={{ fontWeight: step.state === 'pending' ? 400 : 600, color: step.state === 'pending' ? p.text.secondary : p.text.primary, fontSize:'0.9375rem' }}>{step.label}</Typography>
                {step.state === 'active' && <Chip label="ACTIVE" size="small" color="primary" sx={{ height:18, fontSize:'0.625rem' }} />}
                {step.state === 'error' && <Chip label="FAILED" size="small" color="severity.error" sx={{ height:18, fontSize:'0.625rem' }} />}
              </Stack>
              {step.sublabel && <Typography variant="body2" color="secondary" sx={{ marginBottom: 6 }}>{step.sublabel}</Typography>}
              {(step.timestamp || step.source) && (
                <Stack direction="row" spacing={2} alignItems="center" sx={{ marginTop: 4 }}>
                  {step.timestamp && (
                    <Stack direction="row" spacing={0.5} alignItems="center">
                      <Icon name="schedule" size={14} sx={{ color: p.text.secondary }} />
                      <Typography variant="caption" color="secondary" sx={{ fontFamily: p.mode ? '"JetBrains Mono",monospace' : 'inherit', fontSize:'0.75rem' }}>{step.timestamp}</Typography>
                    </Stack>
                  )}
                  {step.source && <Chip label={step.source} variant="outlined" size="small" sx={{ height:18, fontSize:'0.6875rem' }} />}
                </Stack>
              )}
              {step.error && (
                <div style={{ marginTop:8, padding:'8px 12px', background: p.severity.error.bg, border:`1px solid ${p.severity.error.main}40`, borderRadius:6 }}>
                  <Typography variant="caption" sx={{ color: p.severity.error.main, fontFamily:'"JetBrains Mono",monospace' }}>{step.error}</Typography>
                </div>
              )}
            </div>
          </div>
        );
      })}
    </div>
  );
}

// ─── Sparkline (compact, no axes — @mui/x-charts compact mode) ──────────────
function Sparkline({ data, width = 240, height = 60, color, area = true, showDots = false }) {
  const { p } = useTheme();
  const stroke = color || p.primary.main;
  const min = Math.min(...data), max = Math.max(...data), range = max - min || 1;
  const points = data.map((v, i) => [
    (i / (data.length - 1)) * width,
    height - ((v - min) / range) * (height - 8) - 4,
  ]);
  const pathD = points.map((pt, i) => (i === 0 ? `M${pt[0]},${pt[1]}` : `L${pt[0]},${pt[1]}`)).join(' ');
  const areaD = `${pathD} L${width},${height} L0,${height} Z`;
  return (
    <svg width={width} height={height} viewBox={`0 0 ${width} ${height}`} style={{ display:'block' }}>
      <defs>
        <linearGradient id={`spark-${stroke.replace('#','')}`} x1="0" y1="0" x2="0" y2="1">
          <stop offset="0%" stopColor={stroke} stopOpacity="0.25" />
          <stop offset="100%" stopColor={stroke} stopOpacity="0" />
        </linearGradient>
      </defs>
      {area && <path d={areaD} fill={`url(#spark-${stroke.replace('#','')})`} />}
      <path d={pathD} fill="none" stroke={stroke} strokeWidth="1.75" strokeLinejoin="round" strokeLinecap="round" />
      {showDots && points.map((pt, i) => (
        <circle key={i} cx={pt[0]} cy={pt[1]} r={i === points.length - 1 ? 3 : 0} fill={stroke} />
      ))}
    </svg>
  );
}

// ─── Donut (@mui/x-charts PieChart in donut mode) ───────────────────────────
function Donut({ data, size = 220, innerRadius = 0.6, palette }) {
  const { p } = useTheme();
  const colors = palette || [p.primary.main, p.secondary.main, p.severity.warning.main, p.severity.success.main, p.severity.info.main, p.primary.light, p.secondary.light];
  const total = data.reduce((s, d) => s + d.value, 0);
  const cx = size / 2, cy = size / 2, r = size / 2 - 4, ir = r * innerRadius;
  let a0 = -Math.PI / 2;
  return (
    <svg width={size} height={size} viewBox={`0 0 ${size} ${size}`} style={{ display:'block' }}>
      {data.map((d, i) => {
        const a1 = a0 + (d.value / total) * Math.PI * 2;
        const large = a1 - a0 > Math.PI ? 1 : 0;
        const x0 = cx + Math.cos(a0) * r, y0 = cy + Math.sin(a0) * r;
        const x1 = cx + Math.cos(a1) * r, y1 = cy + Math.sin(a1) * r;
        const xi1 = cx + Math.cos(a1) * ir, yi1 = cy + Math.sin(a1) * ir;
        const xi0 = cx + Math.cos(a0) * ir, yi0 = cy + Math.sin(a0) * ir;
        const path = `M${x0},${y0} A${r},${r} 0 ${large} 1 ${x1},${y1} L${xi1},${yi1} A${ir},${ir} 0 ${large} 0 ${xi0},${yi0} Z`;
        a0 = a1;
        return <path key={i} d={path} fill={d.color || colors[i % colors.length]} stroke={p.background.paper} strokeWidth="2" />;
      })}
    </svg>
  );
}

// ─── Score bar (custom: inline mini-bar for DataGrid score cell) ───────────
function ScoreBar({ score, width = 60 }) {
  const { p } = useTheme();
  const s = p.score;
  const c = score < 20 ? s.s0 : score < 40 ? s.s25 : score < 60 ? s.s50 : score < 80 ? s.s75 : s.s100;
  return (
    <Stack direction="row" spacing={1} alignItems="center" sx={{ width: '100%' }}>
      <span style={{ fontFamily:'"JetBrains Mono",monospace', fontSize:'0.8125rem', fontWeight:600, color: c, width: 28, textAlign:'right' }}>{score}</span>
      <div style={{ flex:1, height:6, background: p.action.hover, borderRadius:3, overflow:'hidden', minWidth: width }}>
        <div style={{ height:'100%', width:`${score}%`, background: c, borderRadius:3 }} />
      </div>
    </Stack>
  );
}

// ─── LinearProgress (component-breakdown mini-bars on P2P pool) ────────────
function MiniBar({ value, max, color, width = 50, height = 4 }) {
  const { p } = useTheme();
  return (
    <div style={{ width, height, background: p.action.hover, borderRadius: 2, overflow:'hidden' }}>
      <div style={{ height:'100%', width:`${Math.min(100, (value/max)*100)}%`, background: color }} />
    </div>
  );
}

// ─── Snackbar + Alert ───────────────────────────────────────────────────────
function Snackbar({ severity = 'info', title, message, action, sx = {} }) {
  const { p } = useTheme();
  const tok = p.severity[severity];
  return (
    <div style={{
      display:'flex', alignItems:'flex-start', gap:12, padding:'12px 16px',
      background: p.background.elev2, border:`1px solid ${p.divider}`, borderLeft:`4px solid ${tok.main}`,
      borderRadius: 8, boxShadow: '0 8px 24px rgba(0,0,0,0.32)', minWidth: 360,
      ...sx,
    }}>
      <Icon name={severity === 'error' ? 'error' : severity === 'warning' ? 'warning' : severity === 'success' ? 'check_circle' : 'info'} size={20} sx={{ color: tok.main, marginTop:1 }} />
      <div style={{ flex:1, minWidth:0 }}>
        {title && <Typography variant="subtitle2" sx={{ fontWeight:600, marginBottom: 2 }}>{title}</Typography>}
        <Typography variant="body2" color="secondary">{message}</Typography>
      </div>
      {action}
      <IconButton name="close" size="small" />
    </div>
  );
}

Object.assign(window, { Autocomplete, AppBar, Toolbar, Drawer, ConsigliereSidebar, DataGrid, Stepper, Sparkline, Donut, ScoreBar, MiniBar, Snackbar });
