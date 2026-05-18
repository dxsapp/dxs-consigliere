// frames-notes.jsx — Per-screen interaction notes artboard. Engineering-facing
// prose linking each visual to the concrete MUI primitive + variant, hover/
// focus/active states, framer-motion transitions, real-time data behavior,
// and the per-widget stale-state appearance.

function NotesFrame({ width = 1200 }) {
  const palette = ConsigliereThemeConfig.palette.dark;
  const ctx = { p: palette, t: ConsigliereThemeConfig.typography, density: 'comfortable' };
  return (
    <ThemeCtx.Provider value={ctx}>
      <div style={{ width, background: palette.background.default, color: palette.text.primary, fontFamily: 'Roboto, sans-serif', padding: 40 }}>
        <div style={{ marginBottom: 32 }}>
          <Typography variant="overline" color="secondary">Interaction notes</Typography>
          <Typography variant="h3" sx={{ fontWeight:400, marginTop: 4 }}>Per-screen handoff</Typography>
          <Typography variant="body1" color="secondary" sx={{ marginTop: 8, maxWidth: 760 }}>
            For each hi-fi screen: the MUI variant chosen for every interactive component, hover / focus / active / disabled states (MUI handles most — call-outs only where custom), framer-motion transitions, real-time data behaviour, and the per-widget stale overlay.
          </Typography>
        </div>

        <Stack spacing={4}>
          <NoteSection
            title="1 · Dashboard (4 variants)"
            chips={['Sidebar: Drawer permanent','Search: Autocomplete freeSolo','Sparklines: x-charts LineChart compact','Activity stream: List + ListItem','Hero metric: Typography h2 + h3']}
            blocks={[
              { h:'MUI variants used',
                items:[
                  '<Drawer variant="permanent"> width 240px desktop; swapped to variant="temporary" + AppBar IconButton on mobile (md breakpoint).',
                  '<AppBar position="sticky" color="transparent" elevation={0}> with custom Toolbar — 64px desktop / 56px mobile / 48px when density=dense.',
                  '<Autocomplete freeSolo controlled> for header search; size="small" in AppBar, size="medium" + variant="filled" for the centered double-entry search inside the hero Card.',
                  'Hero KPI: Typography variant="h2" weight 300 over Typography variant="overline". Sub-metrics: 4 × icon-tile + h6 + caption.',
                  'Sparkline: LineChart from @mui/x-charts in compact mode (no axis, single line, last-point Dot). One per metric Card.',
                  'Recent-lookup chips: <Chip variant="outlined" size="small"> in a Stack direction="row" with flexWrap.',
                  'Activity stream: <List dense={density==="dense"}> + <ListItem> with <ListItemIcon> (source icon) + primary text (txid in code variant) + state <Chip color="severity.*">.',
                ]},
              { h:'States',
                items:[
                  'AppBar IconButton hover: MUI default action.hover. Notification badge uses <Badge overlap="circular" color="error">.',
                  'Recent-lookup Chip hover: deletable affordance reveals X icon (onDelete prop wired).',
                  'Sparkline last-point dot pulses 1.2× scale every 5s while data is fresh; disabled while stale overlay is on.',
                ]},
              { h:'framer-motion',
                items:[
                  'Sparkline new tick → variants={{ enter:{pathLength:1}, exit:{pathLength:0} }} transitions over 400ms with ease="easeOut". Path interpolates between successive snapshots.',
                  'KPI value change → motion.span with key={value} and AnimatePresence mode="popLayout" → vertical slide-in (initial:{y:8,opacity:0} animate:{y:0,opacity:1}).',
                  'Activity stream new item → motion.li with layout + initial={{height:0,opacity:0}} animate={{height:"auto",opacity:1}}.',
                ]},
              { h:'Real-time + stale',
                items:[
                  'SignalR OnBroadcastStateChanged → store mutates → MobX observer rerenders affected ListItem only.',
                  'When the dashboard MobX store enters stale = true (SignalR disconnect OR poll fail), each widget Card wraps its body in <Box sx={{ opacity: 0.4, pointerEvents:"none" }}> + corner <Chip label="stale" icon={Warning} color="warning" variant="outlined">. The header AppBar SignalR chip flips from "live" (success) to "reconnecting…" (warning).',
                ]},
            ]}
          />

          <NoteSection
            title="2 · Transaction detail"
            chips={['Stepper orientation vertical','Custom StepIcon + StepContent','Tabs (uncontrolled)','code Typography variant','severity Chip color']}
            blocks={[
              { h:'MUI variants used',
                items:[
                  '<Stepper orientation="vertical" activeStep={4}> with overridden <StepIcon> (custom: completed=check / active=autorenew pulsing / pending=numbered outlined / error=close).',
                  'Each <StepContent> renders metadata block: timestamp Stack + <Chip variant="outlined"> source + optional error panel (severity.error.bg with main border).',
                  '<Tabs variant="standard" indicatorColor="primary" textColor="primary"> at the projection-state section; 5 tabs.',
                  '<Button variant="outlined" size="small" startIcon={...}> for "Copy txid" — only Copy is contained-equivalent in importance.',
                  '<Typography variant="code"> custom variant for all hex/txid rendering (registered via createTheme typography.code).',
                ]},
              { h:'framer-motion',
                items:[
                  'Active stage transitions to done → custom StepIcon AnimatePresence: check icon scales from 0 → 1 over 300ms cubic-bezier(.2,.7,.3,1); connector line above it animates from divider color → severity.success.main (pathLength 0 → 1, 500ms).',
                  'New stage becomes active → outer halo box-shadow animates (boxShadow: `0 0 0 0 ${color}33` → `0 0 0 6px ${color}33`), 600ms, then settles.',
                ]},
              { h:'Real-time + stale',
                items:[
                  'SignalR delivers the next OutgoingTxState transition → MobX store appends to txDetail.steps[] → React re-renders, framer-motion picks up new active step.',
                  'Stale: per-widget overlay on the timeline Card only; secondary metadata Cards stay live (they\'re backed by REST one-shot).',
                ]},
            ]}
          />

          <NoteSection
            title="3 · Broadcast Queue (kanban)"
            chips={['3 × Card columns','AnimatePresence layoutId','Snackbar + Alert + Button action','Dialog for confirm rebroadcast']}
            blocks={[
              { h:'MUI variants used',
                items:[
                  'Each column = <Card variant="outlined"> with a colored top border (severity.info / .warning / .success) + header Stack with column-count <Chip>.',
                  'Per-card: <Card variant="outlined" sx={{ borderLeft: `3px solid ${columnColor}` }}> with <CardContent> rendering txid (code), age, source/relay chip, last-transition timestamp.',
                  'Stale card override: borderColor + boxShadow → severity.warning; inline alert strip inside the card body.',
                  'Card overflow → <IconButton><MoreHoriz/></IconButton> opens <Menu> with single <MenuItem onClick> "Force rebroadcast" — opens <Dialog> with <DialogActions> "Cancel" (text) + "Re-announce" (contained color="warning").',
                  '<Snackbar anchorOrigin={{ vertical:"bottom", horizontal:"right" }} autoHideDuration={8000}> wrapping <Alert severity="warning" action={<Button color="warning" size="small">Force rebroadcast</Button>}>.',
                ]},
              { h:'framer-motion',
                items:[
                  '<AnimatePresence mode="popLayout"> wraps each column\'s card list. Each card uses motion.div with layoutId={txid}.',
                  'On state transition (e.g. Dispatching → PeerRelayed): store removes card from col A, adds to col B with same layoutId. framer-motion animates layout shift across columns over 450ms ease="easeInOut".',
                  'Insertion of net-new card (Validated): initial={{ scale: 0.92, opacity: 0 }} animate={{ scale: 1, opacity: 1 }}.',
                ]},
              { h:'Real-time + stale',
                items:[
                  'Cards age in place — a setInterval(1s) ticks elapsedSinceTransition. Cross-threshold at 5 min → store marks card stale → motion.div animate.borderColor transitions to warning over 600ms; stale Alert strip slides in beneath txid (initial={{height:0}}).',
                  'When the SignalR hub drops, the whole kanban Card body gets the stale overlay (0.4 opacity + corner chip); per-card stale flagging is independent and survives because it\'s derived from local clock.',
                ]},
              { h:'Destructive action — the only one',
                items:[
                  'Confirmation copy: "Re-announce {txid}? Current state: Dispatching, {ageMin} minutes old." Default-focused button = Cancel.',
                  'After confirm: POST /api/tx/broadcast { rawHex } resubmitted (idempotent, W5 A2 M1 fix). UI shows transient <Snackbar severity="info" message="Rebroadcast queued — receipt {txid}">.',
                ]},
            ]}
          />

          <NoteSection
            title="4 · Alerts"
            chips={['Card stack (active)','DataGrid (history)','severity color tokens','filter Chip onDelete']}
            blocks={[
              { h:'MUI variants used',
                items:[
                  'Active alerts grid: 2-column grid of <Card variant="outlined" sx={{ borderLeft: `4px solid ${severity.main}` }}> with a subtle gradient header (severity.bg → background.paper at 240px).',
                  'Each active card: icon (Error/Warning) + monospace rule type + severity Chip; detail text; fired-at timestamp Stack; context dict in a key/value grid wrapped in a code-styled panel.',
                  'History: <DataGrid density={density==="dense"?"compact":"standard"} rows={alerts} columns={[sev, type, ts, detail, docId, open]} disableRowSelectionOnClick>. Quick-filter Chips (onDelete) above.',
                  'No ack action — journal is read-only this wave per brief §7.',
                ]},
              { h:'framer-motion',
                items:[
                  'New alert fired → top-row toast <Snackbar> slides in from top-right (initial={{x:24,opacity:0}} animate={{x:0,opacity:1}}, 280ms).',
                  'New row prepended to history DataGrid: subtle background flash (severity.bg → transparent over 1200ms).',
                ]},
              { h:'Real-time + stale',
                items:[
                  '60s poll cadence on /api/admin/p2p/alerts?since=lastSeenMs. No SignalR push for alerts in W6.',
                  'Stale: whole-page overlay only on the active-alerts section if the poll has been failing for >2 cycles; history grid stays live because operator may still need to read past entries.',
                ]},
            ]}
          />

          <NoteSection
            title="5 · P2P Pool"
            chips={['DataGrid 60-70%','PieChart donut 30-40%','custom score gradient','MiniBar score components']}
            blocks={[
              { h:'MUI variants used',
                items:[
                  '<DataGrid density={...} disableRowSelectionOnClick sortModel default score asc> — exposing rotation candidates at the top.',
                  'Score cell: custom renderCell with monospace numeric + <LinearProgress variant="determinate" value={score}> styled via score-gradient color picker.',
                  'Components cell (Latency · Reject · RelayBack): three stacked mini bars (custom <Box> styled per token — these don\'t need to be a separate primitive, they\'re sx-only inside a renderCell).',
                  'Donut: <PieChart series={[{ data, innerRadius: 60, paddingAngle: 1 }]} width={220} height={220} hideLegend /> with center label rendered via absolute-positioned Typography.',
                  'KPI strip: 4 × KPI Card (icon tile + overline + h3 + caption) reusing the Dashboard sub-metric pattern.',
                ]},
              { h:'framer-motion',
                items:[
                  'Per-tick score recompute → MiniBar widths animate over 600ms ease-out. Score gradient color crossfades when the bucket changes (e.g. 30 → 28 crosses the s0/s25 boundary).',
                  'Peer eviction (rotation tick decides to drop a peer): row exit animate={{ opacity:0, height:0 }} → DataGrid layout snaps. Inbound peer row entrance: initial={{ opacity:0, x:-12 }}.',
                ]},
              { h:'Real-time + stale',
                items:[
                  '/api/admin/p2p/health + /peers polled every 5s; SignalR OnNewBlock triggers an immediate re-pull.',
                  'Stale overlay applies to the DataGrid Card and the Donut Card independently — they have separate data sources (peers list vs health summary).',
                ]},
            ]}
          />

          <NoteSection
            title="Global · cross-screen behaviours"
            chips={['Theme toggle','Density toggle','Sidebar System divider','Toast queue']}
            blocks={[
              { h:'Theme + density toggles',
                items:[
                  'Both live in the bottom of the Drawer + the right of the AppBar (redundant by request — operator may have the AppBar partly hidden when narrow).',
                  'Theme toggle: <IconButton><LightMode/DarkMode/></IconButton> → flips appStore.themeMode → ThemeProvider rerenders. Persisted via mobx-persist-store.',
                  'Density toggle: <IconButton><DensityMedium/></IconButton> → flips appStore.density → MuiDataGrid default density + custom --row-height token used by List sx. Persisted same way.',
                ]},
              { h:'System section in Sidebar',
                items:[
                  'Visual separation via a labeled <Divider><DividerText>System</DividerText></Divider>. No collapse toggle — always visible per brief §2.',
                  'Each System item carries a small "DEV" <Chip variant="outlined" size="small"> to the right of the label. Identical hover/selected behaviour to Operator items.',
                ]},
              { h:'Toast queue',
                items:[
                  'Single <SnackbarProvider maxSnack=3 anchorOrigin={{ vertical:"bottom", horizontal:"right" }}> at app root. Auto-hide 8000ms, severity colour = alert.severity, click navigates to /alerts.',
                  'Stuck-broadcast toast carries a "Force rebroadcast" <Button> in the Alert action prop — same Dialog as the kanban card overflow menu.',
                ]},
            ]}
          />
        </Stack>
      </div>
    </ThemeCtx.Provider>
  );
}

function NoteSection({ title, chips, blocks }) {
  const { p } = useTheme();
  return (
    <Card variant="outlined" sx={{ padding: 28 }}>
      <Stack direction="row" alignItems="center" justifyContent="space-between" sx={{ marginBottom: 8 }}>
        <Typography variant="h4" sx={{ fontWeight:500, fontSize:'1.375rem' }}>{title}</Typography>
      </Stack>
      <Stack direction="row" spacing={1} wrap sx={{ marginBottom: 20 }}>
        {chips.map((c,i) => <Chip key={i} label={c} size="small" variant="outlined" />)}
      </Stack>
      <Stack spacing={2.5}>
        {blocks.map((b, i) => (
          <div key={i}>
            <Typography variant="overline" sx={{ color: p.primary.main, fontWeight: 600, display:'block', marginBottom: 6 }}>{b.h}</Typography>
            <ul style={{ margin: 0, padding: 0, listStyle: 'none' }}>
              {b.items.map((it, j) => (
                <li key={j} style={{ display:'flex', gap: 10, marginBottom: 6, padding:'8px 12px', background: p.mode==='dark' ? p.background.elev1 : p.background.elev2, borderRadius:6 }}>
                  <span style={{ color: p.primary.main, marginTop:2 }}>›</span>
                  <Typography variant="body2" sx={{ flex: 1, color: p.text.primary, lineHeight: 1.6 }}>{it}</Typography>
                </li>
              ))}
            </ul>
          </div>
        ))}
      </Stack>
    </Card>
  );
}

Object.assign(window, { NotesFrame });
