import {
  Alert,
  Card,
  CardContent,
  Chip,
  Stack,
  Typography,
} from "@mui/material";
import { observer } from "mobx-react-lite";
import { useEffect, useMemo } from "react";
import { useParams } from "react-router-dom";
import { EntityTimeline } from "@/screens/entity-detail/EntityTimeline";
import { TransactionDetailStore } from "@/screens/entity-detail/transaction-detail.store";
import type { EventBus } from "@/lib/events/bus";
import type { ISignalRClient } from "@/lib/signalr/client";
import { isTxStateFailure, type OutgoingTxState } from "@/types/admin";

/**
 * S5 — TX detail screen.
 *
 * The Stepper is the focal point; we drive it from the live bus
 * because the backend currently does not expose a REST endpoint for
 * outgoing-tx history (S5 followup). On entry we kick the hub
 * `SubscribeToBroadcast(txId)` so the server starts pushing events.
 */
export const TransactionDetailPage = observer(function TransactionDetailPage({
  bus,
  signalR,
}: {
  bus: EventBus;
  signalR: Pick<ISignalRClient, "subscribeToBroadcast">;
}) {
  const { txid = "" } = useParams<{ txid: string }>();

  const store = useMemo(
    () => new TransactionDetailStore({ txId: txid, bus, signalR }),
    [txid, bus, signalR]
  );

  useEffect(() => {
    void store.start();
    return () => store.dispose();
  }, [store]);

  const latest = store.latest;
  const failed = latest ? isTxStateFailure(latest.state as OutgoingTxState) : false;

  return (
    <Stack spacing={3}>
      <Stack direction="row" alignItems="baseline" spacing={2} flexWrap="wrap" useFlexGap>
        <Typography variant="h4">Transaction</Typography>
        <Chip label="S5" size="small" variant="outlined" />
        {store.isTerminal && (
          <Chip
            label={failed ? "Terminal · failed" : "Terminal · confirmed"}
            size="small"
            color={failed ? "error" : "success"}
          />
        )}
      </Stack>

      <Card>
        <CardContent>
          <Stack spacing={1}>
            <Typography variant="overline" color="text.secondary">
              txid
            </Typography>
            <Typography variant="code" sx={{ wordBreak: "break-all" }}>
              {txid || "(missing)"}
            </Typography>
          </Stack>
        </CardContent>
      </Card>

      {store.subscribeError && (
        <Alert severity="warning">
          Live subscription not active: {store.subscribeError}. Events
          received via the bus are still rendered below.
        </Alert>
      )}

      <Card>
        <CardContent>
          <Stack spacing={2}>
            <Typography variant="overline" color="text.secondary">
              Lifecycle
            </Typography>
            {store.history.length === 0 ? (
              <Typography variant="body2" color="text.secondary">
                Awaiting first state event from the relay coordinator.
                Once the backend emits OnBroadcastStateChanged for this
                txid, the timeline advances in real time.
              </Typography>
            ) : null}
            <EntityTimeline
              stages={store.stagesView}
              ariaLabel="transaction lifecycle"
            />
          </Stack>
        </CardContent>
      </Card>

      {store.history.length > 0 && (
        <Card>
          <CardContent>
            <Stack spacing={1}>
              <Typography variant="overline" color="text.secondary">
                Raw events · newest first
              </Typography>
              {store.history.map((e) => (
                <Stack
                  key={`${e.state}-${e.updatedAtMs}`}
                  direction="row"
                  justifyContent="space-between"
                  alignItems="baseline"
                  spacing={2}
                >
                  <Typography variant="body2">{e.state}</Typography>
                  <Typography variant="caption" color="text.secondary">
                    {new Date(e.updatedAtMs).toUTCString().slice(5, 25)} UTC
                    {e.failReason ? ` · ${e.failReason}` : ""}
                  </Typography>
                </Stack>
              ))}
            </Stack>
          </CardContent>
        </Card>
      )}
    </Stack>
  );
});
