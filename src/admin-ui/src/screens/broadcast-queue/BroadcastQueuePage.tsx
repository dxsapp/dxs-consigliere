import {
  Box,
  Button,
  Card,
  CardContent,
  CardHeader,
  Chip,
  Grid,
  Stack,
  Tooltip,
  Typography,
  useTheme,
} from "@mui/material";
import AccessTimeIcon from "@mui/icons-material/AccessTime";
import OpenInNewIcon from "@mui/icons-material/OpenInNew";
import ReplayIcon from "@mui/icons-material/Replay";
import { AnimatePresence, motion } from "framer-motion";
import { observer } from "mobx-react-lite";
import { useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import { ForceRebroadcastDialog } from "@/screens/broadcast-queue/ForceRebroadcastDialog";
import {
  BroadcastQueueStore,
  type BroadcastLane,
  type QueueCard,
} from "@/screens/broadcast-queue/broadcast-queue.store";
import type { IAdminClient } from "@/lib/admin/admin-client";
import type { EventBus } from "@/lib/events/bus";

const LANES: { id: BroadcastLane; title: string; subtitle: string }[] = [
  { id: "validated", title: "Validated", subtitle: "accepted, not on the wire" },
  { id: "dispatching", title: "Dispatching", subtitle: "INV sent, awaiting relay" },
  { id: "peerRelayed", title: "Peer-relayed", subtitle: "visible in mempool" },
];

/**
 * S6 — operator kanban for in-flight outgoing tx. Reads the live
 * event bus through BroadcastQueueStore; framer-motion's
 * AnimatePresence drives lane-to-lane transitions.
 *
 * Force-rebroadcast is the only destructive action wired here; the
 * dialog is reused for the per-card retry button and the page-level
 * "Force rebroadcast" header CTA (for ad-hoc txs the operator wants
 * to push without first seeing a stale card).
 */
export const BroadcastQueuePage = observer(function BroadcastQueuePage({
  admin,
  bus,
}: {
  admin: IAdminClient;
  bus: EventBus;
}) {
  const store = useMemo(() => new BroadcastQueueStore({ bus }), [bus]);
  useEffect(() => {
    store.start();
    return () => store.dispose();
  }, [store]);

  const [dialogOpen, setDialogOpen] = useState(false);
  const [seedTxId, setSeedTxId] = useState<string | undefined>(undefined);

  const openDialog = (txId?: string) => {
    setSeedTxId(txId);
    setDialogOpen(true);
  };

  return (
    <Stack spacing={3}>
      <Stack
        direction={{ xs: "column", sm: "row" }}
        alignItems={{ sm: "baseline" }}
        spacing={2}
        flexWrap="wrap"
        useFlexGap
      >
        <Typography variant="h4">Broadcast queue</Typography>
        <Chip label="S6" size="small" variant="outlined" />
        <Chip
          label={`${store.totalActive} active`}
          size="small"
          color={store.totalActive > 0 ? "primary" : "default"}
        />
        <Box sx={{ flex: 1 }} />
        <Button
          variant="contained"
          color="warning"
          startIcon={<ReplayIcon />}
          onClick={() => openDialog()}
        >
          Force rebroadcast
        </Button>
      </Stack>

      <Grid container spacing={2}>
        {LANES.map((lane) => (
          <Grid key={lane.id} size={{ xs: 12, md: 4 }}>
            <LaneCard store={store} lane={lane} onForce={openDialog} />
          </Grid>
        ))}
      </Grid>

      <ForceRebroadcastDialog
        admin={admin}
        open={dialogOpen}
        initialTxId={seedTxId}
        onClose={() => setDialogOpen(false)}
      />
    </Stack>
  );
});

const LaneCard = observer(function LaneCard({
  store,
  lane,
  onForce,
}: {
  store: BroadcastQueueStore;
  lane: (typeof LANES)[number];
  onForce: (txId?: string) => void;
}) {
  const items: QueueCard[] = store[lane.id];

  return (
    <Card sx={{ height: "100%" }} data-testid={`lane-${lane.id}`}>
      <CardHeader
        title={
          <Stack direction="row" alignItems="baseline" spacing={1}>
            <Typography variant="h6">{lane.title}</Typography>
            <Chip size="small" label={items.length} />
          </Stack>
        }
        subheader={lane.subtitle}
      />
      <CardContent>
        {items.length === 0 ? (
          <Typography variant="body2" color="text.secondary">
            No cards in this lane.
          </Typography>
        ) : (
          <Stack spacing={1}>
            <AnimatePresence initial={false}>
              {items.map((card) => (
                <motion.div
                  key={card.txId}
                  layout
                  initial={{ opacity: 0, y: -8 }}
                  animate={{ opacity: 1, y: 0 }}
                  exit={{ opacity: 0, y: 8 }}
                  transition={{ type: "spring", stiffness: 480, damping: 30 }}
                >
                  <QueueCardView card={card} store={store} onForce={onForce} />
                </motion.div>
              ))}
            </AnimatePresence>
          </Stack>
        )}
      </CardContent>
    </Card>
  );
});

const QueueCardView = observer(function QueueCardView({
  card,
  store,
  onForce,
}: {
  card: QueueCard;
  store: BroadcastQueueStore;
  onForce: (txId?: string) => void;
}) {
  const theme = useTheme();
  const navigate = useNavigate();
  const stale = store.isStale(card);
  const tone = card.terminal
    ? card.failReason
      ? theme.palette.severity.error.main
      : theme.palette.severity.success.main
    : stale
    ? theme.palette.severity.warning.main
    : theme.palette.divider;

  return (
    <Card
      variant="outlined"
      data-testid={`queue-card-${card.txId}`}
      data-stale={stale ? "true" : "false"}
      sx={{ borderLeft: `4px solid ${tone}`, p: 1 }}
    >
      <Stack direction="row" justifyContent="space-between" alignItems="center" spacing={1}>
        <Stack sx={{ minWidth: 0, flex: 1 }}>
          <Typography variant="code" sx={{ wordBreak: "break-all" }}>
            {card.txId.length > 24 ? `${card.txId.slice(0, 12)}…${card.txId.slice(-8)}` : card.txId}
          </Typography>
          <Typography variant="caption" color="text.secondary">
            {card.state}
            {card.failReason ? ` · ${card.failReason}` : ""}
            {stale ? " · stale" : ""}
          </Typography>
        </Stack>
        <Stack direction="row" spacing={0.5}>
          {stale && (
            <Tooltip title="In dispatching for >5 min">
              <Chip
                icon={<AccessTimeIcon />}
                label="stale"
                size="small"
                color="warning"
              />
            </Tooltip>
          )}
          <Tooltip title="Open tx detail">
            <Button
              size="small"
              onClick={() => navigate(`/transactions/${card.txId}`)}
              startIcon={<OpenInNewIcon />}
            >
              View
            </Button>
          </Tooltip>
          <Tooltip title="Force rebroadcast">
            <Button
              size="small"
              color="warning"
              onClick={() => onForce(card.txId)}
              startIcon={<ReplayIcon />}
            >
              Retry
            </Button>
          </Tooltip>
        </Stack>
      </Stack>
    </Card>
  );
});
