import {
  Alert,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogContentText,
  DialogTitle,
  Stack,
  TextField,
  Typography,
} from "@mui/material";
import { useEffect, useMemo, useRef, useState } from "react";
import type { IAdminClient } from "@/lib/admin/admin-client";
import type { BroadcastReceiptDto } from "@/types/admin";

/**
 * S6 — sole destructive action in the admin UI (design brief §7).
 *
 * Two-step confirmation:
 *  1. Operator pastes a rawHex; we sanity-check shape (even-length,
 *     hex chars, ≥10 bytes).
 *  2. Operator hits CONFIRM; we POST /api/tx/broadcast and show the
 *     resulting BroadcastReceiptDto inline. The dialog stays open
 *     until the operator dismisses — they can copy the receipt txid.
 *
 * The form is intentionally bare: no fancy syntax highlighting, no
 * decode preview. A future S6 followup may add a `parseRaw()` step.
 */
export function ForceRebroadcastDialog({
  admin,
  open,
  initialRawHex,
  initialTxId,
  onClose,
}: {
  admin: IAdminClient;
  open: boolean;
  initialRawHex?: string;
  initialTxId?: string;
  onClose: () => void;
}) {
  const [rawHex, setRawHex] = useState(initialRawHex ?? "");
  const [confirming, setConfirming] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [receipt, setReceipt] = useState<BroadcastReceiptDto | null>(null);
  const [error, setError] = useState<string | null>(null);

  // S6-audit H3: an in-flight broadcastRaw POST is destructive — we
  // cannot close the dialog (and lose the receipt) while it's still
  // running. Track the controller in a ref so unmount + onClose can
  // abort it deterministically.
  const inflight = useRef<AbortController | null>(null);

  useEffect(() => {
    if (!open) {
      // Reset on close so the next opening starts clean.
      setRawHex(initialRawHex ?? "");
      setConfirming(false);
      setSubmitting(false);
      setReceipt(null);
      setError(null);
    }
  }, [open, initialRawHex]);

  // Abort any in-flight request if the component unmounts entirely.
  useEffect(() => {
    return () => {
      inflight.current?.abort();
      inflight.current = null;
    };
  }, []);

  const valid = useMemo(() => isPlausibleRaw(rawHex), [rawHex]);

  const onSubmit = async () => {
    if (!valid || submitting) return;
    setSubmitting(true);
    setError(null);
    const ctl = new AbortController();
    inflight.current = ctl;
    try {
      const res = await admin.broadcastRaw(rawHex.trim(), ctl.signal);
      if (ctl.signal.aborted) return;
      setReceipt(res);
    } catch (err) {
      if (ctl.signal.aborted) return;
      setError(err instanceof Error ? err.message : "Broadcast failed");
    } finally {
      if (inflight.current === ctl) inflight.current = null;
      setSubmitting(false);
    }
  };

  /** Dialog close requested. Block while submit is in flight so the
   *  operator can't accidentally discard the receipt mid-POST. */
  const handleClose = () => {
    if (submitting) return;
    onClose();
  };

  return (
    <Dialog
      open={open}
      onClose={handleClose}
      maxWidth="md"
      fullWidth
      // Avoid backdrop + escape closing while submit is in flight —
      // explicit operator gesture only.
      disableEscapeKeyDown={submitting}
    >
      <DialogTitle>Force rebroadcast</DialogTitle>
      <DialogContent dividers>
        <Stack spacing={2}>
          <DialogContentText>
            POSTs the raw transaction to <code>/api/tx/broadcast</code>.
            This is a destructive operation: the network will see a
            (re)broadcast immediately. Double-check the rawHex.
          </DialogContentText>

          {initialTxId && (
            <Stack spacing={0.5}>
              <Typography variant="overline" color="text.secondary">
                Source txid
              </Typography>
              <Typography variant="code" sx={{ wordBreak: "break-all" }}>
                {initialTxId}
              </Typography>
            </Stack>
          )}

          <TextField
            label="rawHex"
            multiline
            minRows={3}
            maxRows={10}
            fullWidth
            value={rawHex}
            onChange={(e) => setRawHex(e.target.value)}
            placeholder="0100000001…"
            disabled={submitting || receipt !== null}
            error={rawHex.length > 0 && !valid}
            helperText={
              rawHex.length === 0
                ? "Paste the serialised transaction hex."
                : valid
                ? "Looks like valid hex."
                : "Must be even-length, hex-only, ≥20 chars."
            }
          />

          {error && <Alert severity="error">{error}</Alert>}

          {receipt && (
            <Alert severity={receipt.failReason ? "warning" : "success"}>
              <Stack spacing={0.5}>
                <Typography variant="body2">
                  Backend accepted the request.
                </Typography>
                <Typography variant="caption">
                  txid: <code>{receipt.txId}</code>
                </Typography>
                <Typography variant="caption">
                  state: <code>{receipt.state}</code>
                </Typography>
                {receipt.failReason && (
                  <Typography variant="caption">
                    failReason: <code>{receipt.failReason}</code>
                  </Typography>
                )}
              </Stack>
            </Alert>
          )}
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={handleClose} disabled={submitting}>
          {receipt ? "Close" : "Cancel"}
        </Button>
        {!receipt && !confirming && (
          <Button
            color="warning"
            variant="contained"
            disabled={!valid || submitting}
            onClick={() => setConfirming(true)}
          >
            Rebroadcast…
          </Button>
        )}
        {!receipt && confirming && (
          <Button
            color="error"
            variant="contained"
            disabled={!valid || submitting}
            onClick={() => void onSubmit()}
          >
            {submitting ? "Submitting…" : "Confirm broadcast"}
          </Button>
        )}
      </DialogActions>
    </Dialog>
  );
}

function isPlausibleRaw(raw: string): boolean {
  const v = raw.trim();
  if (v.length < 20) return false;
  if (v.length % 2 !== 0) return false;
  return /^[0-9a-fA-F]+$/.test(v);
}
