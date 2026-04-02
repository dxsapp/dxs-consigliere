import { useNavigate } from "react-router-dom";
import { observer } from "mobx-react-lite";
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  IconButton,
  Skeleton,
  Stack,
  Tooltip,
  Typography,
} from "@mui/material";
import RefreshOutlinedIcon from "@mui/icons-material/RefreshOutlined";
import { dashboardStore } from "@/stores/dashboard.store";
import { KeyValueCard } from "@/components/KeyValueCard";
import { SummaryMetricCard } from "@/components/SummaryMetricCard";

function formatCount(value: number | null | undefined): string {
  if (value == null) return "unavailable";
  return new Intl.NumberFormat("en-GB").format(value);
}

function formatPercent(value: number | null | undefined): string {
  if (value == null || Number.isNaN(value)) return "unavailable";
  return `${(value * 100).toFixed(1)}%`;
}

function DashboardSkeleton() {
  return (
    <Stack spacing={2}>
      <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", md: "repeat(4, minmax(0, 1fr))" }, gap: 2 }}>
        {Array.from({ length: 8 }).map((_, index) => (
          <Skeleton key={index} variant="rounded" height={116} />
        ))}
      </Box>
      <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", xl: "repeat(3, minmax(0, 1fr))" }, gap: 2 }}>
        <Skeleton variant="rounded" height={260} />
        <Skeleton variant="rounded" height={260} />
        <Skeleton variant="rounded" height={260} />
      </Box>
    </Stack>
  );
}

export const DashboardPage = observer(function DashboardPage() {
  const navigate = useNavigate();
  const store = dashboardStore;

  if (store.isLoading) {
    return (
      <Box>
        <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", mb: 3 }}>
          <Typography variant="h5" sx={{ fontWeight: 600, letterSpacing: "-0.02em" }}>
            Dashboard
          </Typography>
        </Box>
        <DashboardSkeleton />
      </Box>
    );
  }

  if (store.loadState === "error") {
    return (
      <Box>
        <Typography variant="h5" sx={{ fontWeight: 600, letterSpacing: "-0.02em", mb: 2 }}>
          Dashboard
        </Typography>
        <Alert
          severity="error"
          action={
            <Button color="inherit" size="small" onClick={() => store.reload()}>
              Retry
            </Button>
          }
        >
          {store.error}
        </Alert>
      </Box>
    );
  }

  const summary = store.summary;
  const syncStatus = store.syncStatus;
  const cacheStatus = store.cacheStatus;
  const storageStatus = store.storageStatus;

  return (
    <Box>
      <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", mb: 3 }}>
        <Box>
          <Typography variant="h5" sx={{ fontWeight: 600, letterSpacing: "-0.02em" }}>
            Dashboard
          </Typography>
          <Typography variant="body2" sx={{ color: "text.secondary", mt: 0.5 }}>
            Current managed scope, operator attention items, and a short read on local infrastructure readiness.
          </Typography>
        </Box>
        <Tooltip title="Refresh">
          <IconButton size="small" onClick={() => store.reload()} disabled={store.refreshing} sx={{ color: "text.disabled" }}>
            {store.refreshing ? <CircularProgress size={16} color="inherit" /> : <RefreshOutlinedIcon fontSize="small" />}
          </IconButton>
        </Tooltip>
      </Box>

      <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", md: "repeat(4, minmax(0, 1fr))" }, gap: 2, mb: 3 }}>
        <SummaryMetricCard
          label="Tracked addresses"
          value={formatCount(summary?.activeAddressCount ?? 0)}
          helper={summary && summary.tombstonedAddressCount > 0 ? `${formatCount(summary.tombstonedAddressCount)} tombstoned` : "Open tracked address scope"}
          onClick={() => navigate("/addresses")}
        />
        <SummaryMetricCard
          label="Tracked tokens"
          value={formatCount(summary?.activeTokenCount ?? 0)}
          helper={summary && summary.tombstonedTokenCount > 0 ? `${formatCount(summary.tombstonedTokenCount)} tombstoned` : "Open tracked token scope"}
          onClick={() => navigate("/tokens")}
        />
        <SummaryMetricCard
          label="Failures"
          value={formatCount(summary?.failureCount ?? 0)}
          accent={summary && summary.failureCount > 0 ? "error" : "default"}
          helper={summary && summary.failureCount > 0 ? "Recent operator-visible failures" : "No failure findings in summary"}
          onClick={summary && summary.failureCount > 0 ? () => navigate("/findings") : undefined}
        />
        <SummaryMetricCard
          label="Unknown root findings"
          value={formatCount(summary?.unknownRootFindingCount ?? 0)}
          accent={summary && summary.unknownRootFindingCount > 0 ? "warning" : "default"}
          helper={
            summary && summary.blockingUnknownRootTokenCount > 0
              ? `${formatCount(summary.blockingUnknownRootTokenCount)} blocking tokens`
              : "No blocking rooted-history findings"
          }
          onClick={summary && summary.unknownRootFindingCount > 0 ? () => navigate("/findings") : undefined}
        />
        <SummaryMetricCard
          label="Degraded addresses"
          value={formatCount(summary?.degradedAddressCount ?? 0)}
          accent={summary && summary.degradedAddressCount > 0 ? "error" : "default"}
          helper="Addresses needing operator attention"
          onClick={() => navigate("/addresses")}
        />
        <SummaryMetricCard
          label="Degraded tokens"
          value={formatCount(summary?.degradedTokenCount ?? 0)}
          accent={summary && summary.degradedTokenCount > 0 ? "error" : "default"}
          helper="Tokens needing operator attention"
          onClick={() => navigate("/tokens")}
        />
        <SummaryMetricCard
          label="Address backfill active"
          value={formatCount(summary?.backfillingAddressCount ?? 0)}
          accent={summary && summary.backfillingAddressCount > 0 ? "info" : "default"}
          helper={`${formatCount(summary?.fullHistoryLiveAddressCount ?? 0)} historical backfills live`}
          onClick={() => navigate("/addresses")}
        />
        <SummaryMetricCard
          label="Token rooted backfill active"
          value={formatCount(summary?.backfillingTokenCount ?? 0)}
          accent={summary && summary.backfillingTokenCount > 0 ? "info" : "default"}
          helper={`${formatCount(summary?.fullHistoryLiveTokenCount ?? 0)} rooted backfills live`}
          onClick={() => navigate("/tokens")}
        />
      </Box>

      <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", xl: "repeat(3, minmax(0, 1fr))" }, gap: 2 }}>
        <KeyValueCard
          title="Chain sync"
          description="A short summary of local chain progress. Use Runtime for deeper provider and assurance diagnostics."
          status={{
            label: syncStatus?.isSynced ? "synced" : "catching up",
            color: syncStatus?.isSynced ? "success" : "warning",
          }}
          rows={[
            { label: "Indexed height", value: <Typography variant="body2" sx={{ fontFamily: "monospace" }}>{formatCount(syncStatus?.height ?? null)}</Typography> },
            { label: "Sync status", value: <Typography variant="body2">{syncStatus?.isSynced ? "Local chain tip is caught up." : "Local chain tip is still advancing."}</Typography> },
            { label: "Next surface", value: <Button size="small" onClick={() => navigate("/runtime")}>Open Runtime</Button> },
          ]}
        />

        <KeyValueCard
          title="Projection cache"
          description="Cache health and projection lag for the local read models used by the operator shell."
          status={{
            label: cacheStatus?.enabled ? "enabled" : "disabled",
            color: cacheStatus?.enabled ? "success" : "default",
          }}
          rows={[
            { label: "Backend", value: <Typography variant="body2" sx={{ fontFamily: "monospace" }}>{cacheStatus?.backend ?? "unavailable"}</Typography> },
            { label: "Entries", value: <Typography variant="body2" sx={{ fontFamily: "monospace" }}>{formatCount(cacheStatus?.count ?? null)}{cacheStatus?.maxEntries != null ? ` / ${formatCount(cacheStatus.maxEntries)}` : ""}</Typography> },
            { label: "Hit ratio", value: <Typography variant="body2" sx={{ fontFamily: "monospace" }}>{formatPercent(cacheStatus?.hitRatio ?? null)}</Typography> },
            { label: "Projection lag", value: <Typography variant="body2" sx={{ fontFamily: "monospace" }}>{cacheStatus ? `addr ${formatCount(cacheStatus.projectionLag.address.lag)} · token ${formatCount(cacheStatus.projectionLag.token.lag)} · tx ${formatCount(cacheStatus.projectionLag.txLifecycle.lag)}` : "unavailable"}</Typography> },
            { label: "Next surface", value: <Button size="small" onClick={() => navigate("/storage")}>Open Storage</Button> },
          ]}
        />

        <KeyValueCard
          title="Raw transaction payload storage"
          description="Persistence posture for raw transaction payloads used by repair and replay workflows."
          status={{
            label: storageStatus?.rawTransactionPayloads.persistenceActive ? "active" : "inactive",
            color: storageStatus?.rawTransactionPayloads.persistenceActive ? "success" : "warning",
          }}
          rows={[
            { label: "Provider", value: <Typography variant="body2" sx={{ fontFamily: "monospace" }}>{storageStatus?.rawTransactionPayloads.provider ?? "unavailable"}</Typography> },
            { label: "Retention", value: <Typography variant="body2">{storageStatus?.rawTransactionPayloads.retentionPolicy ?? "unavailable"}</Typography> },
            { label: "Compression", value: <Typography variant="body2">{storageStatus?.rawTransactionPayloads.compression ?? "unavailable"}</Typography> },
            { label: "Location", value: <Typography variant="body2" sx={{ fontFamily: "monospace", wordBreak: "break-all" }}>{storageStatus?.rawTransactionPayloads.location.collection || storageStatus?.rawTransactionPayloads.location.rootPath || storageStatus?.rawTransactionPayloads.location.bucket || "unavailable"}</Typography> },
            { label: "Next surface", value: <Button size="small" onClick={() => navigate("/storage")}>Inspect storage</Button> },
          ]}
        />
      </Box>

      {!cacheStatus?.enabled && (
        <Alert severity="info" sx={{ mt: 2 }}>
          Projection cache is disabled. The operator shell still works, but repeated reads will depend more directly on the backing store.
        </Alert>
      )}

      {storageStatus?.rawTransactionPayloads.notes?.length ? (
        <Alert severity="info" sx={{ mt: 2 }}>
          {storageStatus.rawTransactionPayloads.notes.join(" ")}
        </Alert>
      ) : null}

      <Typography variant="body2" sx={{ color: "text.disabled", mt: 2 }}>
        Cache and storage details are intentionally kept short here. Use Runtime for live diagnostics and Storage for persistence details.
      </Typography>
    </Box>
  );
});
