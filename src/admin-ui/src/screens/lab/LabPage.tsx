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
 * build + sign a P2PKH send client-side → broadcast over the node's own
 * P2P pool → see the receipt. No third-party provider involved.
 *
 * The private key never leaves the browser — only the address (to
 * track) and the signed raw hex (to broadcast) cross the wire.
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

  const address = store.address;

  return (
    <Stack spacing={3}>
      <Stack direction="row" alignItems="baseline" spacing={2} flexWrap="wrap" useFlexGap>
        <Typography variant="h4">Transaction Lab</Typography>
        <Chip label="S1" size="small" variant="outlined" />
      </Stack>

      <Alert severity="warning" variant="outlined">
        <AlertTitle>Lab tool — real mainnet transactions</AlertTitle>
        This builds and broadcasts REAL mainnet transactions with REAL funds.
        Keep amounts small. The private key is generated and signs entirely in
        your browser and is never sent to the backend. It is saved in this
        browser (localStorage) so a refresh keeps the wallet — use{" "}
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
                  disabled={store.generating || store.sending}
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
            title="3 · Send (P2PKH)"
            subheader="Signed in your browser, broadcast over the node's P2P pool. Change returns to the lab address."
          />
          <CardContent>
            <Box
              component="form"
              onSubmit={(e) => {
                e.preventDefault();
                void store.send(destination, Number(amount));
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
                  disabled={store.sending}
                  data-testid="lab-send"
                >
                  {store.sending ? "Signing & broadcasting…" : "Build, sign & broadcast"}
                </Button>
              </Stack>
            </Box>

            {store.receipt &&
              (() => {
                const { state, failReason, txId } = store.receipt;
                const failed = isFailedState(state) || Boolean(failReason);
                return (
                  <Alert
                    severity={failed ? "error" : "success"}
                    sx={{ mt: 3 }}
                    data-testid="lab-receipt"
                  >
                    <AlertTitle>
                      {failed ? "Broadcast rejected" : "Broadcast accepted"} — {state}
                    </AlertTitle>
                    <Stack spacing={0.5}>
                      {failReason && (
                        <Box data-testid="lab-receipt-reason">Reason: {failReason}</Box>
                      )}
                      {txId && (
                        <Box sx={{ fontFamily: "monospace", wordBreak: "break-all" }}>
                          {txId}
                        </Box>
                      )}
                      {address && !failed && (
                        <Link component={RouterLink} to={`/addresses/${address}`}>
                          View tracked lab address
                        </Link>
                      )}
                    </Stack>
                  </Alert>
                );
              })()}
          </CardContent>
        </Card>
      )}
    </Stack>
  );
});

// Terminal failure states of OutgoingTxState (the rest are progressing /
// success states). A receipt is also treated as failed if it carries a
// failReason (e.g. audit_write_failed, p2p_disabled).
const FAILED_STATES = new Set([
  "Failed",
  "PolicyInvalid",
  "InvalidRejected",
  "ConflictRejected",
  "EvictedOrDropped",
  "ObserverUnknown",
]);
function isFailedState(state: string): boolean {
  return FAILED_STATES.has(state);
}

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
