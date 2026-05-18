import { Stack, Typography } from "@mui/material";
import { LineChart } from "@mui/x-charts";
import { observer } from "mobx-react-lite";
import type { SourceMetricsStore } from "@/screens/source-metrics/source-metrics.store";
import { SOURCE_KEYS } from "@/types/admin";

/**
 * S9 — heavy LineChart for the per-source first-seen history. Lazy
 * so @mui/x-charts stays out of the shell (S3-audit M6).
 */
export const SourceMetricsCharts = observer(function SourceMetricsCharts({
  store,
}: {
  store: SourceMetricsStore;
}) {
  const series = SOURCE_KEYS.map((src) => ({
    label: src,
    data: store.firstSeenSeries(src),
    showMark: false,
  }));
  if (series.every((s) => s.data.length === 0)) {
    return (
      <Typography variant="body2" color="text.secondary">
        Waiting for at least two snapshots to derive deltas.
      </Typography>
    );
  }
  const length = Math.max(...series.map((s) => s.data.length));
  const xAxis = Array.from({ length }, (_, i) => i);
  return (
    <Stack data-testid="source-metrics-charts">
      <LineChart
        height={280}
        series={series}
        xAxis={[{ data: xAxis, label: "tick" }]}
      />
    </Stack>
  );
});
