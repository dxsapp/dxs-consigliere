// frames-tx-detail.jsx — Transaction detail screen (hi-fi mock #2).
// Hero element: vertical Stepper timeline of OutgoingTxState transitions
// (Validated → Dispatching → PeerRelayed → Mined → Confirmed) — per the W5
// broadcast contract. Secondary panels below carry entity metadata.

const TX_DETAIL = {
  txid:    'a91f3c12d80b9e4f5c6d7e8a9b0c1d2e3f4a5b6c7d8e9f0a1b2c3d4e5f6a7c4d',
  amount:  '0.04827159 BSV',
  fiat:    '$2,421.85',
  size:    '1,284 bytes',
  feerate: '0.51 sat/byte',
  block:   856221,
  blockHash: '0000000000000000049a7cf2c8b2e5e6f1a2d3c4b5e6f7a89b0c1d2e3f4a5b6c',
  confirmations: 4,
  sources: ['p2p', 'bitails', 'junglebus'],
  rawHexPreview: '0100000001a91f3c12d80b9e4f5c6d7e8a9b0c1d2e3f4a5b6c7d8e9f0a1b2c3d4e5f6a7c4d000000006a47304402...',
};

const TX_STEPS = [
  { label:'Validated',  state:'done', timestamp:'14:23:11.842 UTC (4m 12s ago)', source:'TxPolicyValidator',
    sublabel:'Hex parse OK · 1,284 B ≤ MaxRawSizeBytes 2,097,152 · no existing txid' },
  { label:'Dispatching', state:'done', timestamp:'14:23:11.901 UTC (+59 ms)', source:'BroadcastService',
    sublabel:'8/8 ready peers · announce dispatched to all' },
  { label:'PeerRelayed', state:'done', timestamp:'14:23:12.318 UTC (+417 ms)', source:'inv echo · 6 peers',
    sublabel:'RelayBack: 6 inv echoes from peers within 500 ms · healthy propagation' },
  { label:'Mined',       state:'done', timestamp:'14:25:48.022 UTC (+2m 35s)', source:'p2p',
    sublabel:'Included in block 856,221 · position 47 · coinbase + 312 tx' },
  { label:'Confirmed',   state:'active', timestamp:'in progress · 4 / 6 confirmations',
    sublabel:'4 subsequent blocks observed · 2 more needed for final confirmation' },
];

function TxDetailFrame({ width = 1440, height = 1000, mode = 'dark', density = 'comfortable' }) {
  const palette = ConsigliereThemeConfig.palette[mode];
  const ctx = { p: palette, t: ConsigliereThemeConfig.typography, density };
  return (
    <ThemeCtx.Provider value={ctx}>
      <div style={{ width, height, display:'flex', background: palette.background.default, color: palette.text.primary, fontFamily: 'Roboto, sans-serif', overflow:'hidden' }}>
        <ConsigliereSidebar selected="tx" density={density} />

        <div style={{ flex:1, display:'flex', flexDirection:'column', minWidth:0 }}>
          <AppBar density={density}>
            <Toolbar sx={{ padding:'0 24px' }}>
              <Autocomplete value={TX_DETAIL.txid.slice(0,18)+'…'+TX_DETAIL.txid.slice(-8)} sx={{ width: 480, maxWidth:'40%' }} />
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

          <div style={{ flex:1, overflow:'auto', padding: 24, display:'flex', flexDirection:'column', gap: 24 }}>
            {/* Breadcrumb + header */}
            <div>
              <Stack direction="row" alignItems="center" spacing={1} sx={{ marginBottom: 12, color: palette.text.secondary, fontSize:'0.875rem' }}>
                <Icon name="swap_horiz" size={16} sx={{ color: palette.text.secondary }} />
                <span style={{ color: palette.text.secondary }}>Transactions</span>
                <Icon name="chevron_right" size={16} sx={{ color: palette.text.secondary }} />
                <span style={{ fontFamily:'"JetBrains Mono",monospace', color: palette.text.primary }}>{TX_DETAIL.txid.slice(0,16)}…</span>
              </Stack>
              <Stack direction="row" alignItems="center" spacing={2} sx={{ marginBottom: 4 }}>
                <Typography variant="overline" color="secondary">Transaction</Typography>
                <Chip label="CONFIRMED" size="small" color="success" />
                <Chip label="4 / 6 confirmations" size="small" variant="outlined" color="info" />
              </Stack>
              <Typography variant="code" component="div" sx={{ fontSize:'1.0625rem', wordBreak:'break-all', marginTop: 4 }}>
                {TX_DETAIL.txid}
              </Typography>
              <Stack direction="row" alignItems="center" spacing={1} sx={{ marginTop: 8 }}>
                <Button variant="outlined" startIcon={<Icon name="content_copy" size={16} />}>Copy txid</Button>
                <Button variant="text" startIcon={<Icon name="open_in_new" size={16} />}>Raw hex</Button>
                <Button variant="text" startIcon={<Icon name="data_object" size={16} />}>Raven doc</Button>
              </Stack>
            </div>

            {/* Two-column hero: Timeline + Metadata */}
            <div style={{ display:'grid', gridTemplateColumns:'1.4fr 1fr', gap: 24 }}>
              <Card variant="outlined">
                <CardHeader
                  title="Broadcast lifecycle"
                  subheader="OutgoingTxState transitions · live via SignalR OnBroadcastStateChanged"
                  action={<Chip label="LIVE" size="small" color="success" variant="outlined" icon="circle" />}
                />
                <Divider />
                <div style={{ padding: '24px 24px 8px 24px' }}>
                  <Stepper steps={TX_STEPS} />
                </div>
              </Card>

              <Stack spacing={2}>
                <Card variant="outlined">
                  <CardHeader title="Amount" />
                  <CardContent>
                    <Typography variant="h3" sx={{ fontFamily:'"JetBrains Mono",monospace', fontWeight:500, fontSize:'1.75rem' }}>{TX_DETAIL.amount}</Typography>
                    <Typography variant="body2" color="secondary">≈ {TX_DETAIL.fiat}</Typography>
                  </CardContent>
                </Card>
                <Card variant="outlined">
                  <CardHeader title="Sources observed" subheader="Order of first-seen across the 3 ingest paths" />
                  <CardContent>
                    <Stack direction="row" spacing={1} wrap>
                      <Chip label="1. p2p · 14:23:11.901 UTC" variant="outlined" size="small" icon="hub" color="primary" />
                      <Chip label="2. bitails · +0.4s" variant="outlined" size="small" icon="public" />
                      <Chip label="3. junglebus · +1.2s" variant="outlined" size="small" icon="travel_explore" />
                    </Stack>
                  </CardContent>
                </Card>
                <Card variant="outlined">
                  <CardHeader title="Block context" subheader="W1 headers tip" />
                  <CardContent>
                    <Stack spacing={1}>
                      <Stack direction="row" justifyContent="space-between"><Typography variant="caption" color="secondary">Height</Typography><Typography variant="body2" sx={{ fontFamily:'"JetBrains Mono",monospace' }}>{TX_DETAIL.block.toLocaleString()}</Typography></Stack>
                      <Stack direction="row" justifyContent="space-between"><Typography variant="caption" color="secondary">Block hash</Typography><Typography variant="body2" sx={{ fontFamily:'"JetBrains Mono",monospace', fontSize:'0.75rem' }}>{TX_DETAIL.blockHash.slice(0,12)}…{TX_DETAIL.blockHash.slice(-6)}</Typography></Stack>
                      <Stack direction="row" justifyContent="space-between"><Typography variant="caption" color="secondary">Position in block</Typography><Typography variant="body2" sx={{ fontFamily:'"JetBrains Mono",monospace' }}>47 / 313</Typography></Stack>
                      <Stack direction="row" justifyContent="space-between"><Typography variant="caption" color="secondary">Size</Typography><Typography variant="body2" sx={{ fontFamily:'"JetBrains Mono",monospace' }}>{TX_DETAIL.size}</Typography></Stack>
                      <Stack direction="row" justifyContent="space-between"><Typography variant="caption" color="secondary">Fee rate</Typography><Typography variant="body2" sx={{ fontFamily:'"JetBrains Mono",monospace' }}>{TX_DETAIL.feerate}</Typography></Stack>
                    </Stack>
                  </CardContent>
                </Card>
              </Stack>
            </div>

            {/* Tabs + projection state */}
            <Card variant="outlined">
              <div style={{ display:'flex', alignItems:'center', borderBottom:`1px solid ${palette.divider}`, padding:'0 16px' }}>
                {['Projection state','Inputs (1)','Outputs (2)','Raw hex','OutgoingTransaction doc'].map((t, i) => (
                  <div key={t} style={{
                    padding:'14px 16px', cursor:'pointer',
                    borderBottom: i === 0 ? `2px solid ${palette.primary.main}` : '2px solid transparent',
                    color: i === 0 ? palette.primary.main : palette.text.secondary, fontWeight: i === 0 ? 600 : 400, fontSize:'0.875rem',
                    textTransform:'uppercase', letterSpacing:'0.4px',
                  }}>{t}</div>
                ))}
              </div>
              <div style={{ padding: 20 }}>
                <Typography variant="overline" color="secondary" sx={{ marginBottom: 8, display:'block' }}>Raw hex preview · first 128 bytes</Typography>
                <div style={{ padding: 16, background: palette.mode==='dark'?'#0B0E12':'#F7F8FA', borderRadius:6, border:`1px solid ${palette.divider}`, fontFamily:'"JetBrains Mono",monospace', fontSize:'0.8125rem', color: palette.text.secondary, wordBreak:'break-all', lineHeight: 1.7 }}>
                  {TX_DETAIL.rawHexPreview}
                  <span style={{ color: palette.text.disabled }}> … +1,156 bytes</span>
                </div>
              </div>
            </Card>
          </div>
        </div>
      </div>
    </ThemeCtx.Provider>
  );
}

Object.assign(window, { TxDetailFrame });
