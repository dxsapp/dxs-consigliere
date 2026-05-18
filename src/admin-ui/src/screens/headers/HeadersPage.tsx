import {
  Alert,
  Card,
  CardContent,
  CardHeader,
  Chip,
  Divider,
  LinearProgress,
  Stack,
  Typography,
} from "@mui/material";
import { observer } from "mobx-react-lite";
import { useEffect, useMemo } from "react";
import { HeadersStore } from "@/screens/headers/headers.store";
import type { IAdminClient } from "@/lib/admin/admin-client";
import type { EventBus } from "@/lib/events/bus";

export const HeadersPage = observer(function HeadersPage({
  admin,
  bus,
}: {
  admin: IAdminClient;
  bus: EventBus;
}) {
  const store = useMemo(() => new HeadersStore({ admin, bus }), [admin, bus]);
  useEffect(() => {
    void store.start();
    return () => store.dispose();
  }, [store]);

  return (
    <Stack spacing={3}>
      <Stack direction="row" alignItems="baseline" spacing={2} flexWrap="wrap" useFlexGap>
        <Typography variant="h4">Headers chain</Typography>
        <Chip label="S9" size="small" variant="outlined" />
        {store.tip && (
          <Chip label={`height ${store.tip.height}`} size="small" color="primary" />
        )}
      </Stack>

      {store.status === "loading" && !store.tip && <LinearProgress />}
      {store.error && (
        <Alert severity="error">Headers fetch failed: {store.error}</Alert>
      )}

      <Card>
        <CardHeader title="Tip" />
        <CardContent>
          {store.tip ? (
            <Stack spacing={1}>
              <Row k="hash" v={store.tip.hash} mono />
              <Row k="prevHash" v={store.tip.prevHash} mono />
              <Row k="height" v={`${store.tip.height}`} />
              <Row
                k="timestamp"
                v={`${new Date(store.tip.timestampMs).toUTCString().slice(5, 25)} UTC`}
              />
            </Stack>
          ) : (
            <Typography variant="body2" color="text.secondary">
              No tip yet — backend is still bootstrapping the headers chain.
            </Typography>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader
          title="Recent headers"
          subheader={`${store.recent.length} entries · newest first`}
        />
        <CardContent>
          {store.recent.length === 0 ? (
            <Typography variant="body2" color="text.secondary">
              No history yet.
            </Typography>
          ) : (
            <Stack divider={<Divider flexItem />}>
              {store.recent.map((h) => (
                <Stack
                  key={h.hash}
                  direction="row"
                  justifyContent="space-between"
                  alignItems="baseline"
                  spacing={2}
                  sx={{ py: 0.75 }}
                >
                  <Typography variant="code" sx={{ wordBreak: "break-all" }}>
                    {h.hash.slice(0, 16)}…{h.hash.slice(-8)}
                  </Typography>
                  <Stack direction="row" spacing={1} alignItems="baseline">
                    <Typography variant="body2">h{h.height}</Typography>
                    <Typography variant="caption" color="text.secondary">
                      {new Date(h.timestampMs).toUTCString().slice(5, 22)}
                    </Typography>
                  </Stack>
                </Stack>
              ))}
            </Stack>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader
          title="Reorgs"
          subheader="bus.OnReorg events since this view mounted"
        />
        <CardContent>
          {store.reorgEvents.length === 0 ? (
            <Typography variant="body2" color="text.secondary">
              No reorgs observed.
            </Typography>
          ) : (
            <Stack spacing={1}>
              {store.reorgEvents.map((r) => (
                <Alert
                  key={`${r.newTipHash}-${r.observedAtMs}`}
                  severity={r.degradedState ? "error" : "warning"}
                >
                  Reorg at ancestor h{r.commonAncestorHeight} → new tip h{r.newTipHeight} ·
                  {" "}orphaned {r.orphanedHashes.length} block(s)
                  {r.degradedState ? " · DEGRADED" : ""}
                </Alert>
              ))}
            </Stack>
          )}
        </CardContent>
      </Card>
    </Stack>
  );
});

function Row({ k, v, mono = false }: { k: string; v: string; mono?: boolean }) {
  return (
    <Stack direction="row" justifyContent="space-between" alignItems="baseline" spacing={2}>
      <Typography variant="overline" color="text.secondary">
        {k}
      </Typography>
      <Typography variant={mono ? "code" : "body2"} sx={{ wordBreak: "break-all", textAlign: "right" }}>
        {v}
      </Typography>
    </Stack>
  );
}
