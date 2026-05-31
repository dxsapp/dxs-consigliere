import {
  Alert,
  Button,
  Card,
  CardContent,
  CardHeader,
  Chip,
  CircularProgress,
  LinearProgress,
  Stack,
  TextField,
  Typography,
} from "@mui/material";
import CheckCircleIcon from "@mui/icons-material/CheckCircle";
import RadioButtonUncheckedIcon from "@mui/icons-material/RadioButtonUnchecked";
import ReplayIcon from "@mui/icons-material/Replay";
import { observer } from "mobx-react-lite";
import { useEffect, useMemo, useState } from "react";
import { EntityTimeline } from "@/screens/entity-detail/EntityTimeline";
import { TransactionDetailStore } from "@/screens/entity-detail/transaction-detail.store";
import { ExternalSightingsStore } from "@/screens/broadcast-inspector/external-sightings.store";
import type { IAdminClient } from "@/lib/admin/admin-client";
import type { EventBus } from "@/lib/events/bus";
import type { ISignalRClient } from "@/lib/signalr/client";
import type { BroadcastReceiptDto } from "@/types/admin";

/**
 * S9 — Broadcast Inspector. Lets the operator submit a raw tx and
 * watch its lifecycle inline. Reuses the S5 EntityTimeline +
 * TransactionDetailStore so the projection logic stays single-
 * sourced.
 */
export const BroadcastInspectorPage = observer(function BroadcastInspectorPage({
  admin,
  bus,
  signalR,
}: {
  admin: IAdminClient;
  bus: EventBus;
  signalR: Pick<ISignalRClient, "subscribeToBroadcast">;
}) {
  const [rawHex, setRawHex] = useState("");
  const [receipt, setReceipt] = useState<BroadcastReceiptDto | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const txId = receipt?.txId ?? "";
  const store = useMemo(
    () => (txId ? new TransactionDetailStore({ txId, bus, signalR }) : null),
    [txId, bus, signalR]
  );
  useEffect(() => {
    if (!store) return;
    void store.start();
    return () => store.dispose();
  }, [store]);

  const valid = isPlausibleRaw(rawHex);
  const onSubmit = async () => {
    if (!valid || submitting) return;
    setSubmitting(true);
    setError(null);
    setReceipt(null);
    try {
      const res = await admin.broadcastRaw(rawHex.trim());
      setReceipt(res);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Broadcast failed");
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Stack spacing={3}>
      <Stack direction="row" alignItems="baseline" spacing={2} flexWrap="wrap" useFlexGap>
        <Typography variant="h4">Broadcast inspector</Typography>
        <Chip label="S9" size="small" variant="outlined" />
      </Stack>

      <Card>
        <CardHeader title="Submit" subheader="POST /api/tx/broadcast" />
        <CardContent>
          <Stack spacing={2}>
            <TextField
              label="rawHex"
              multiline
              minRows={3}
              maxRows={10}
              fullWidth
              value={rawHex}
              onChange={(e) => setRawHex(e.target.value)}
              placeholder="0100000001…"
              disabled={submitting}
              error={rawHex.length > 0 && !valid}
              helperText={
                rawHex.length === 0
                  ? "Paste a raw tx hex."
                  : valid
                  ? "Looks like valid hex."
                  : "Must be even-length, hex-only, ≥20 chars."
              }
            />
            <Stack direction="row" spacing={2} alignItems="center">
              <Button
                variant="contained"
                color="primary"
                startIcon={<ReplayIcon />}
                disabled={!valid || submitting}
                onClick={() => void onSubmit()}
              >
                {submitting ? "Submitting…" : "Submit + watch"}
              </Button>
              {submitting && <LinearProgress sx={{ flex: 1 }} />}
              {error && <Alert severity="error" sx={{ flex: 1 }}>{error}</Alert>}
            </Stack>
          </Stack>
        </CardContent>
      </Card>

      {receipt && (
        <Card>
          <CardHeader
            title="Receipt"
            subheader={`txid ${receipt.txId.slice(0, 16)}… · state ${receipt.state}`}
          />
          <CardContent>
            <Stack spacing={2}>
              <Typography variant="code" sx={{ wordBreak: "break-all" }}>
                {receipt.txId}
              </Typography>
              {receipt.failReason && (
                <Alert severity="warning">failReason: {receipt.failReason}</Alert>
              )}
              {store && (
                <Stack spacing={0.5}>
                  <Stack direction="row" justifyContent="space-between" alignItems="baseline">
                    <Typography variant="body2" color="text.secondary">
                      Step {store.progress.stepNumber} of {store.progress.totalSteps} ·{" "}
                      {store.progress.label}
                    </Typography>
                    <Typography variant="caption" color="text.secondary">
                      {store.progress.percent}%
                    </Typography>
                  </Stack>
                  <LinearProgress
                    variant="determinate"
                    value={store.progress.percent}
                    color={store.progress.failed ? "error" : "primary"}
                  />
                </Stack>
              )}
              {store && (
                <EntityTimeline
                  stages={store.stagesView}
                  ariaLabel="broadcast inspector lifecycle"
                />
              )}
              {store?.subscribeError && (
                <Alert severity="warning">
                  Live subscription not active: {store.subscribeError}
                </Alert>
              )}
              <ExternalSightings admin={admin} txId={receipt.txId} />
            </Stack>
          </CardContent>
        </Card>
      )}
    </Stack>
  );
});

/**
 * Independent third-party confirmation: after a broadcast, poll WhatsOnChain
 * / Bitails / JungleBus once a second each until they report the txid as
 * seen. Proves a P2P-broadcast tx (no third-party submission) reached the
 * public indexers. Each row stops polling the moment it's seen.
 */
const ExternalSightings = observer(function ExternalSightings({
  admin,
  txId,
}: {
  admin: Pick<IAdminClient, "getExternalSighting">;
  txId: string;
}) {
  const store = useMemo(
    () => new ExternalSightingsStore({ admin, txId }),
    [admin, txId]
  );
  useEffect(() => {
    store.start();
    return () => store.dispose();
  }, [store]);

  return (
    <Stack spacing={1}>
      <Typography variant="overline" color="text.secondary">
        Independent explorer confirmation
      </Typography>
      {store.sightings.map((s) => (
        <Stack
          key={s.source}
          direction="row"
          alignItems="center"
          spacing={1}
          data-testid={`sighting-${s.source}`}
          data-seen={s.seen}
        >
          {s.seen ? (
            <CheckCircleIcon fontSize="small" color="success" />
          ) : s.polling ? (
            <CircularProgress size={16} />
          ) : (
            <RadioButtonUncheckedIcon fontSize="small" color="disabled" />
          )}
          <Typography variant="body2">{s.label}</Typography>
          <Typography variant="caption" color="text.secondary">
            {s.seen ? "seen" : s.polling ? "checking…" : "not seen"}
          </Typography>
        </Stack>
      ))}
    </Stack>
  );
});

function isPlausibleRaw(raw: string): boolean {
  const v = raw.trim();
  if (v.length < 20) return false;
  if (v.length % 2 !== 0) return false;
  return /^[0-9a-fA-F]+$/.test(v);
}
