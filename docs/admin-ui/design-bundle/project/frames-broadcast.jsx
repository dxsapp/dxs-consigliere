// frames-broadcast.jsx — Broadcast Queue kanban (hi-fi mock #3).
// 3 columns: Validated → Dispatching → PeerRelayed. Cards move between
// columns in real time via SignalR OnBroadcastStateChanged.
// One card in Dispatching has been there >5 min and is stale-highlighted
// (the bottleneck signal). Card overflow menu carries the only destructive
// action in the whole UI: "Force rebroadcast".

const QUEUE = {
  validated: [
    { txid:'b2c389f1d0…04ee', age:'3s',  source:'—',     ts:'14:27:11.842' },
    { txid:'77a4b08c12…f2a1', age:'19s', source:'—',     ts:'14:26:55.118' },
    { txid:'4f8e22ad8c…cc91', age:'48s', source:'—',     ts:'14:26:26.301' },
  ],
  dispatching: [
    { txid:'04b29fa6c1…91a0', age:'1m 04s', source:'7/8 peers', ts:'14:26:10.220' },
    { txid:'9bdc8e2200…1f3e', age:'1m 38s', source:'8/8 peers', ts:'14:25:36.541' },
    { txid:'12aa3401b2…8847', age:'2m 11s', source:'8/8 peers', ts:'14:25:03.802' },
    // Stale — over 5 min in Dispatching. Highlight with severity.warning.
    { txid:'ee5544fc09…00c3', age:'7m 22s', source:'0/8 peers · reconnecting', ts:'14:19:52.443', stale: true },
  ],
  peerRelayed: [
    { txid:'a91f3c12d8…7c4d', age:'4m 12s', source:'6 inv echo', ts:'14:23:11.901', relayBack: 6 },
    { txid:'7b3a8801ff…22ee', age:'5m 38s', source:'5 inv echo', ts:'14:21:44.103', relayBack: 5 },
    { txid:'1cc890af4b…44de', age:'8m 02s', source:'7 inv echo', ts:'14:19:21.448', relayBack: 7 },
  ],
};

function BroadcastQueueFrame({ width = 1440, height = 900, mode = 'dark', density = 'comfortable' }) {
  const palette = ConsigliereThemeConfig.palette[mode];
  const ctx = { p: palette, t: ConsigliereThemeConfig.typography, density };
  return (
    <ThemeCtx.Provider value={ctx}>
      <div style={{ width, height, display:'flex', background: palette.background.default, color: palette.text.primary, fontFamily: 'Roboto, sans-serif', overflow:'hidden' }}>
        <ConsigliereSidebar selected="queue" density={density} />

        <div style={{ flex:1, display:'flex', flexDirection:'column', minWidth:0 }}>
          <AppBar density={density}>
            <Toolbar sx={{ padding:'0 24px' }}>
              <Autocomplete placeholder="Search tx · address · block · token …" sx={{ width: 480, maxWidth:'40%' }} />
              <div style={{ flex:1 }} />
              <Stack direction="row" alignItems="center" spacing={1}>
                <Chip label="mainnet" size="small" color="success" variant="outlined" icon="public" />
                <Chip label="SignalR · live" size="small" variant="outlined" icon="bolt" sx={{ color: palette.severity.success.main, borderColor: palette.severity.success.main + '60' }} />
              </Stack>
              <IconButton name="notifications" badge={3} />
              <IconButton name="light_mode" />
              <IconButton name="density_medium" />
              <IconButton name="account_circle" />
            </Toolbar>
          </AppBar>

          <div style={{ flex:1, overflow:'auto', padding: 24, display:'flex', flexDirection:'column', gap: 16 }}>
            {/* Page header */}
            <Stack direction="row" alignItems="center" justifyContent="space-between">
              <div>
                <Stack direction="row" alignItems="center" spacing={1.5} sx={{ marginBottom: 4 }}>
                  <Typography variant="h4" sx={{ fontWeight:500 }}>Broadcast Queue</Typography>
                  <Chip label="LIVE" size="small" color="success" variant="outlined" icon="circle" />
                  <Chip label="1 stale · >5m" size="small" color="warning" />
                </Stack>
                <Typography variant="body2" color="secondary">
                  In-flight OutgoingTransaction docs · grouped by state · cards animate on SignalR <code style={{ fontFamily:'"JetBrains Mono",monospace' }}>OnBroadcastStateChanged</code>
                </Typography>
              </div>
              <Stack direction="row" spacing={1}>
                <Button variant="outlined" startIcon={<Icon name="filter_list" size={16} />}>Filter</Button>
                <Button variant="outlined" startIcon={<Icon name="schedule" size={16} />}>Last 15 min</Button>
                <Button variant="contained" startIcon={<Icon name="play_circle" size={16} />}>Open Inspector</Button>
              </Stack>
            </Stack>

            {/* Kanban */}
            <div style={{ display:'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: 16, flex:1, minHeight: 0 }}>
              <KanbanColumn
                title="Validated"
                subtitle="Hex parsed · TxPolicy passed · awaiting dispatch"
                count={QUEUE.validated.length}
                accent={palette.severity.info.main}
                icon="check_circle"
                cards={QUEUE.validated}
              />
              <KanbanColumn
                title="Dispatching"
                subtitle="Announce in flight · waiting on peer relay"
                count={QUEUE.dispatching.length}
                accent={palette.severity.warning.main}
                icon="cell_tower"
                cards={QUEUE.dispatching}
              />
              <KanbanColumn
                title="PeerRelayed"
                subtitle="Inv echoed by peers · awaiting block inclusion"
                count={QUEUE.peerRelayed.length}
                accent={palette.severity.success.main}
                icon="task_alt"
                cards={QUEUE.peerRelayed}
              />
            </div>

            {/* Snackbar / toast — pinned bottom-right per UX brief §6.5 */}
            <div style={{ position:'absolute', bottom: 24, right: 24, zIndex: 1400 }}>
              <Snackbar
                severity="warning"
                title="Tx stuck in Dispatching for 7m 22s"
                message="ee5544fc09…00c3 — peer pool dropped to 0 ready. The W3 lifecycle monitor will retry on next reconnect."
                action={<Button variant="text" color="warning" size="small">Force rebroadcast</Button>}
              />
            </div>
          </div>
        </div>
      </div>
    </ThemeCtx.Provider>
  );
}

function KanbanColumn({ title, subtitle, count, accent, icon, cards }) {
  const { p } = useTheme();
  return (
    <div style={{
      background: p.background.paper, border:`1px solid ${p.divider}`,
      borderRadius: 8, display:'flex', flexDirection:'column', minHeight:0,
    }}>
      <div style={{ padding: '14px 16px', borderBottom:`1px solid ${p.divider}`, display:'flex', flexDirection:'column', gap:4, borderTop:`3px solid ${accent}`, borderTopLeftRadius:8, borderTopRightRadius:8 }}>
        <Stack direction="row" alignItems="center" spacing={1}>
          <Icon name={icon} size={18} sx={{ color: accent }} />
          <Typography variant="subtitle1" sx={{ fontWeight:600, fontSize:'0.9375rem', flex:1 }}>{title}</Typography>
          <Chip label={count} size="small" sx={{ background: accent + '22', color: accent, fontWeight: 600, height: 22 }} />
        </Stack>
        <Typography variant="caption" color="secondary">{subtitle}</Typography>
      </div>
      <div style={{ flex:1, padding: 12, overflow:'auto', display:'flex', flexDirection:'column', gap: 10 }}>
        {cards.map((c, i) => <KanbanCard key={i} card={c} accent={accent} />)}
        {/* Drop-target placeholder for AnimatePresence */}
        <div style={{ height: 6, borderRadius: 3, border: `1px dashed ${p.divider}`, background:'transparent' }} />
      </div>
    </div>
  );
}

function KanbanCard({ card, accent }) {
  const { p } = useTheme();
  const isStale = card.stale;
  const cardBg = p.mode === 'dark' ? p.background.elev1 : p.background.paper;
  return (
    <div style={{
      position:'relative',
      background: cardBg,
      border: isStale ? `1.5px solid ${p.severity.warning.main}` : `1px solid ${p.divider}`,
      borderLeft: `3px solid ${isStale ? p.severity.warning.main : accent}`,
      borderRadius: 8, padding: '10px 12px',
      boxShadow: isStale ? `0 0 0 4px ${p.severity.warning.main}1a` : 'none',
      display:'flex', flexDirection:'column', gap: 6,
    }}>
      <Stack direction="row" alignItems="center" spacing={1}>
        <Typography variant="code" sx={{ flex:1, fontSize:'0.8125rem', color: p.text.primary }}>{card.txid}</Typography>
        <IconButton name="more_horiz" size="small" sx={{ width:24, height:24 }} />
      </Stack>
      <Stack direction="row" alignItems="center" spacing={1}>
        <Stack direction="row" alignItems="center" spacing={0.5} sx={{ flex:1 }}>
          <Icon name="schedule" size={13} sx={{ color: isStale ? p.severity.warning.main : p.text.secondary }} />
          <Typography variant="caption" sx={{ color: isStale ? p.severity.warning.main : p.text.secondary, fontFamily:'"JetBrains Mono",monospace', fontWeight: isStale ? 600 : 400 }}>{card.age}</Typography>
        </Stack>
        {card.relayBack != null
          ? <Chip label={`${card.relayBack} echo`} size="small" variant="outlined" sx={{ height: 18, fontSize: '0.625rem' }} icon="repeat" />
          : <Chip label={card.source} size="small" variant="outlined" sx={{ height: 18, fontSize: '0.625rem' }} />}
      </Stack>
      <Typography variant="caption" color="secondary" sx={{ fontFamily:'"JetBrains Mono",monospace', fontSize:'0.6875rem' }}>last transition {card.ts}</Typography>
      {isStale && (
        <div style={{ marginTop: 4, padding:'6px 8px', background: p.severity.warning.bg, borderRadius:4, border:`1px solid ${p.severity.warning.main}40` }}>
          <Stack direction="row" alignItems="center" spacing={0.5}>
            <Icon name="warning" size={14} sx={{ color: p.severity.warning.main }} />
            <Typography variant="caption" sx={{ color: p.severity.warning.main, fontWeight: 500 }}>STALE · stuck in Dispatching</Typography>
          </Stack>
        </div>
      )}
    </div>
  );
}

Object.assign(window, { BroadcastQueueFrame });
