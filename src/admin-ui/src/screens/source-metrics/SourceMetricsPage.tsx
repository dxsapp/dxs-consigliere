import {
  Alert,
  Card,
  CardContent,
  CardHeader,
  Chip,
  Grid,
  LinearProgress,
  Stack,
  Typography,
} from "@mui/material";
import { observer } from "mobx-react-lite";
import { lazy, Suspense, useEffect, useMemo } from "react";
import { Sparkline } from "@/screens/dashboard/components/Sparkline";
import { SourceMetricsStore } from "@/screens/source-metrics/source-metrics.store";
import type { IAdminClient } from "@/lib/admin/admin-client";
import { SOURCE_KEYS, type SourceKey } from "@/types/admin";

const SourceMetricsCharts = lazy(() =>
  import("@/screens/source-metrics/SourceMetricsCharts").then((m) => ({
    default: m.SourceMetricsCharts,
  }))
);

const SOURCE_LABEL: Record<SourceKey, string> = {
  p2p: "BSV P2P",
  bitails: "Bitails",
  junglebus: "JungleBus",
};

export const SourceMetricsPage = observer(function SourceMetricsPage({
  admin,
}: {
  admin: IAdminClient;
}) {
  const store = useMemo(() => new SourceMetricsStore({ admin }), [admin]);
  useEffect(() => {
    void store.start();
    return () => store.dispose();
  }, [store]);

  return (
    <Stack spacing={3}>
      <Stack direction="row" alignItems="baseline" spacing={2} flexWrap="wrap" useFlexGap>
        <Typography variant="h4">Source metrics</Typography>
        <Chip label="S9" size="small" variant="outlined" />
        {store.metrics?.history.length ? (
          <Chip
            label={`history ${store.metrics.history.length} snapshots`}
            size="small"
            variant="outlined"
          />
        ) : null}
      </Stack>

      {store.status === "loading" && !store.metrics && <LinearProgress />}
      {store.error && (
        <Alert severity="error">Source metrics poll failed: {store.error}</Alert>
      )}

      <Grid container spacing={3}>
        {SOURCE_KEYS.map((src) => {
          const series = store.firstSeenSeries(src);
          const latest = store.latestForSource(src);
          return (
            <Grid key={src} size={{ xs: 12, md: 4 }}>
              <Card>
                <CardHeader
                  title={SOURCE_LABEL[src]}
                  subheader={src}
                  action={
                    <Chip
                      size="small"
                      color={latest && latest.invObserved > 0 ? "success" : "default"}
                      label={latest ? `${latest.matched}/${latest.invObserved} matched` : "no data"}
                    />
                  }
                />
                <CardContent>
                  <Stack spacing={1}>
                    <Typography variant="overline" color="text.secondary">
                      first-seen delta · sparkline
                    </Typography>
                    <Sparkline data={series} width={320} height={48} ariaLabel={`${SOURCE_LABEL[src]} first-seen sparkline`} />
                    {latest && (
                      <Stack direction="row" spacing={1} useFlexGap flexWrap="wrap">
                        <Chip size="small" label={`parseErr ${latest.parseError}`} />
                        <Chip size="small" label={`rate-limited ${latest.rateLimited}`} />
                        <Chip size="small" label={`timeouts ${latest.getDataTimeout}`} />
                        <Chip size="small" label={`oversize ${latest.oversizePayload}`} />
                      </Stack>
                    )}
                  </Stack>
                </CardContent>
              </Card>
            </Grid>
          );
        })}
      </Grid>

      <Card>
        <CardHeader title="History" subheader="aggregated counters over the last 24 snapshots" />
        <CardContent>
          <Suspense fallback={<LinearProgress />}>
            <SourceMetricsCharts store={store} />
          </Suspense>
        </CardContent>
      </Card>
    </Stack>
  );
});
