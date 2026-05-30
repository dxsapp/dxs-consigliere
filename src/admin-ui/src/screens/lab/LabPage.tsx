import {
  Alert,
  AlertTitle,
  Box,
  Button,
  Card,
  CardContent,
  CardHeader,
  Chip,
  IconButton,
  InputAdornment,
  Link,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  TextField,
  Tooltip,
  Typography,
} from "@mui/material";
import ContentCopyIcon from "@mui/icons-material/ContentCopy";
import VisibilityIcon from "@mui/icons-material/Visibility";
import VisibilityOffIcon from "@mui/icons-material/VisibilityOff";
import { observer } from "mobx-react-lite";
import { useEffect, useMemo, useState } from "react";
import { Link as RouterLink } from "react-router-dom";
import { LabStore } from "@/screens/lab/lab.store";
import type { IAdminClient } from "@/lib/admin/admin-client";

/**
 * tx-lab S1 — Transaction Lab. Demonstrates the self-contained loop:
 * generate a key in-browser → its address is auto-tracked → fund it →
 * build + sign a P2PKH send client-side → copy the raw hex into the
 * Broadcast inspector. No third-party provider involved.
 *
 * The lab builds + signs but does NOT broadcast: the operator pastes the
 * raw hex into the Broadcast inspector and watches the round-trip there.
 * The private key never leaves the browser — only the address (to track)
 * crosses the wire from here.
 */
export const LabPage = observer(function LabPage({ admin }: { admin: IAdminClient }) {
  const store = useMemo(() => new LabStore({ admin }), [admin]);
  useEffect(() => {
    // Restore a persisted wallet's UTXOs on mount; abort on unmount.
    void store.start();
    return () => store.dispose();
  }, [store]);

  const [revealWif, setRevealWif] = useState(false);
  const [destination, setDestination] = useState("");
  const [amount, setAmount] = useState("");

  return (
    <Stack spacing={3}>
      <Stack direction="row" alignItems="baseline" spacing={2} flexWrap="wrap" useFlexGap>
        <Typography variant="h4">Transaction Lab</Typography>
        <Chip label="S1" size="small" variant="outlined" />
      </Stack>

      <Alert severity="warning" variant="outlined">
        <AlertTitle>Lab tool — real mainnet transactions</AlertTitle>
        This builds and signs REAL mainnet transactions with REAL funds. Keep
        amounts small. The private key is generated and signs entirely in your
        browser and is never sent to the backend. It is saved in this browser
        (localStorage) so a refresh keeps the wallet — use{" "}
        <strong>Reset wallet</strong> to wipe it. Demo-grade keys; not for
        custody.
      </Alert>

      {store.error && <Alert severity="error">{store.error}</Alert>}

      <Card>
        <CardHeader
          title="1 · Generate a key"
          subheader="A fresh P2PKH keypair is created in your browser and its address is auto-tracked by the node."
        />
        <CardContent>
          <Stack spacing={2}>
            <Stack direction="row" spacing={2} flexWrap="wrap" useFlexGap>
              <Button
                variant="contained"
                onClick={() => void store.generate()}
                disabled={store.generating}
                data-testid="lab-generate"
              >
                {store.generating
                  ? "Generating…"
                  : store.key
                    ? "Regenerate key"
                    : "Generate key"}
              </Button>
              {store.key && (
                <Button
                  variant="outlined"
                  color="warning"
                  onClick={() => store.reset()}
                  disabled={store.generating || store.building}
                  data-testid="lab-reset"
                >
                  Reset wallet
                </Button>
              )}
            </Stack>

            {store.key && (
              <Stack spacing={2}>
                <TextField
                  label="Address (tracked)"
                  value={store.key.address}
                  fullWidth
                  slotProps={{ input: { readOnly: true } }}
                  InputProps={{
                    endAdornment: (
                      <CopyButton value={store.key.address} title="Copy address" />
                    ),
                  }}
                  data-testid="lab-address"
                />
                <TextField
                  label="Private key (WIF) — stays in your browser"
                  value={store.key.wif}
                  type={revealWif ? "text" : "password"}
                  fullWidth
                  slotProps={{ input: { readOnly: true } }}
                  InputProps={{
                    endAdornment: (
                      <InputAdornment position="end">
                        <Tooltip title={revealWif ? "Hide" : "Reveal"}>
                          <IconButton
                            onClick={() => setRevealWif((v) => !v)}
                            edge="end"
                            data-testid="lab-reveal-wif"
                          >
                            {revealWif ? <VisibilityOffIcon /> : <VisibilityIcon />}
                          </IconButton>
                        </Tooltip>
                        <CopyButton value={store.key.wif} title="Copy WIF" />
                      </InputAdornment>
                    ),
                  }}
                  data-testid="lab-wif"
                />
              </Stack>
            )}
          </Stack>
        </CardContent>
      </Card>

      {store.key && (
        <Card>
          <CardHeader
            title="2 · Fund &amp; balance"
            subheader="Send some BSV to the address above, then refresh to see the coins."
            action={
              <Button
                onClick={() => void store.refresh()}
                disabled={store.refreshing}
                data-testid="lab-refresh"
              >
                {store.refreshing ? "Refreshing…" : "Refresh"}
              </Button>
            }
          />
          <CardContent>
            <Stack spacing={2}>
              <Typography variant="h6" data-testid="lab-balance">
                Balance: {store.balanceSats.toLocaleString()} sats
              </Typography>
              {store.utxos.length === 0 ? (
                <Typography color="text.secondary">
                  No spendable UTXOs yet — fund the address and refresh.
                </Typography>
              ) : (
                <Table size="small">
                  <TableHead>
                    <TableRow>
                      <TableCell>TxId</TableCell>
                      <TableCell align="right">Vout</TableCell>
                      <TableCell align="right">Satoshis</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {store.utxos.map((u) => (
                      <TableRow key={`${u.txId}:${u.vout}`}>
                        <TableCell sx={{ fontFamily: "monospace", wordBreak: "break-all" }}>
                          {u.txId}
                        </TableCell>
                        <TableCell align="right">{u.vout}</TableCell>
                        <TableCell align="right">{u.satoshis.toLocaleString()}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </Stack>
          </CardContent>
        </Card>
      )}

      {store.key && (
        <Card>
          <CardHeader
            title="3 · Create &amp; sign (P2PKH)"
            subheader="Built and signed in your browser. Change returns to the lab address. The lab does not broadcast — copy the raw hex below into the Broadcast inspector."
          />
          <CardContent>
            <Box
              component="form"
              onSubmit={(e) => {
                e.preventDefault();
                void store.createAndSign(destination, Number(amount));
              }}
            >
              <Stack spacing={2}>
                <TextField
                  label="Destination address"
                  value={destination}
                  onChange={(e) => setDestination(e.target.value)}
                  fullWidth
                  data-testid="lab-destination"
                />
                <TextField
                  label="Amount (satoshis)"
                  value={amount}
                  onChange={(e) => setAmount(e.target.value)}
                  type="number"
                  fullWidth
                  data-testid="lab-amount"
                />
                <Button
                  type="submit"
                  variant="contained"
                  disabled={store.building}
                  data-testid="lab-create-sign"
                >
                  {store.building ? "Signing…" : "Create & sign"}
                </Button>
              </Stack>
            </Box>

            {store.signedHex && (
              <Stack spacing={1} sx={{ mt: 3 }} data-testid="lab-signed-tx">
                <Typography variant="subtitle2">
                  Signed raw transaction
                </Typography>
                <TextField
                  value={store.signedHex}
                  fullWidth
                  multiline
                  minRows={3}
                  slotProps={{ input: { readOnly: true } }}
                  InputProps={{
                    sx: { fontFamily: "monospace", wordBreak: "break-all" },
                    endAdornment: (
                      <CopyButton value={store.signedHex} title="Copy raw hex" />
                    ),
                  }}
                  data-testid="lab-signed-hex"
                />
                <Typography variant="body2" color="text.secondary">
                  Copy this hex and paste it into the{" "}
                  <Link component={RouterLink} to="/broadcast-inspector">
                    Broadcast inspector
                  </Link>{" "}
                  to send it over the node&apos;s P2P pool. The balance above
                  updates once peers accept and relay the tx back.
                </Typography>
              </Stack>
            )}
          </CardContent>
        </Card>
      )}
    </Stack>
  );
});

function CopyButton({ value, title }: { value: string; title: string }) {
  return (
    <InputAdornment position="end">
      <Tooltip title={title}>
        <IconButton
          edge="end"
          onClick={() => {
            void navigator.clipboard?.writeText(value);
          }}
        >
          <ContentCopyIcon fontSize="small" />
        </IconButton>
      </Tooltip>
    </InputAdornment>
  );
}
