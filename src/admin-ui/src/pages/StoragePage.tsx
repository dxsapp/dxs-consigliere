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
import { opsStore } from "@/stores/ops.store";
import { JsonPanel } from "@/components/JsonPanel";
import { KeyValueCard } from "@/components/KeyValueCard";

function formatCount(value: number | null | undefined): string {
  if (value == null) return "unavailable";
  return new Intl.NumberFormat("en-GB").format(value);
}

function formatPercent(value: number | null | undefined): string {
  if (value == null || Number.isNaN(value)) return "unavailable";
  return `${(value * 100).toFixed(1)}%`;
}

function formatDateTime(value: string | number | null | undefined): string {
  if (!value) return "unavailable";
  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) return "unavailable";
  return new Intl.DateTimeFormat("en-GB", { dateStyle: "medium", timeStyle: "short" }).format(parsed);
}

export const StoragePage = observer(function StoragePage() {
  const store = opsStore;
  const cacheStatus = store.adminCacheStatus;
  const storageStatus = store.adminStorageStatus;

  return (
    <Box>
      <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", mb: 3 }}>
        <Box>
          <Typography variant="h5" sx={{ fontWeight: 600, letterSpacing: "-0.02em" }}>
            Storage
          </Typography>
          <Typography variant="body2" sx={{ color: "text.secondary", mt: 0.5 }}>
            Local persistence, projection cache posture, and raw transaction payload retention details.
          </Typography>
        </Box>
        <Tooltip title="Refresh">
          <IconButton size="small" onClick={() => store.reload()} disabled={store.isLoading || store.refreshing} sx={{ color: "text.disabled" }}>
            {store.refreshing ? <CircularProgress size={16} color="inherit" /> : <RefreshOutlinedIcon fontSize="small" />}
          </IconButton>
        </Tooltip>
      </Box>

      {store.loadState === "error" && (
        <Alert
          severity="error"
          action={
            <Button color="inherit" size="small" onClick={() => store.reload()}>
              Retry
            </Button>
          }
          sx={{ mb: 2 }}
        >
          {store.error}
        </Alert>
      )}

      {store.isLoading && !store.refreshing ? (
        <Stack spacing={2}>
          <Skeleton variant="rounded" height={220} />
          <Skeleton variant="rounded" height={220} />
          <Skeleton variant="rounded" height={160} />
        </Stack>
      ) : (
        <Stack spacing={2}>
          <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", xl: "repeat(2, minmax(0, 1fr))" }, gap: 2 }}>
            <KeyValueCard
              title="Projection cache"
              description="Read-model cache used by the operator shell. Lag here usually points to projection backlog rather than provider drift."
              status={{
                label: cacheStatus?.enabled ? "enabled" : "disabled",
                color: cacheStatus?.enabled ? "success" : "default",
              }}
              rows={[
                { label: "Backend", value: <Typography variant="body2" sx={{ fontFamily: "monospace" }}>{cacheStatus?.backend ?? "unavailable"}</Typography> },
                { label: "Entries", value: <Typography variant="body2" sx={{ fontFamily: "monospace" }}>{formatCount(cacheStatus?.count ?? null)}{cacheStatus?.maxEntries != null ? ` / ${formatCount(cacheStatus.maxEntries)}` : ""}</Typography> },
                { label: "Hit ratio", value: <Typography variant="body2" sx={{ fontFamily: "monospace" }}>{formatPercent(cacheStatus?.hitRatio ?? null)}</Typography> },
                { label: "Invalidations", value: <Typography variant="body2" sx={{ fontFamily: "monospace" }}>{cacheStatus ? `${formatCount(cacheStatus.invalidatedKeys)} keys · ${formatCount(cacheStatus.invalidatedTags)} tags` : "unavailable"}</Typography> },
                { label: "Last invalidation", value: <Typography variant="body2" sx={{ fontFamily: "monospace" }}>{formatDateTime(cacheStatus?.invalidation.lastInvalidatedAt)}</Typography> },
              ]}
            />

            <KeyValueCard
              title="Projection lag"
              description="How far address, token, and lifecycle projections trail the journal tail."
              rows={[
                { label: "Journal tail", value: <Typography variant="body2" sx={{ fontFamily: "monospace" }}>{formatCount(cacheStatus?.projectionLag.journalTailSequence ?? null)}</Typography> },
                { label: "Address lag", value: <Typography variant="body2" sx={{ fontFamily: "monospace" }}>{formatCount(cacheStatus?.projectionLag.address.lag ?? null)}</Typography> },
                { label: "Token lag", value: <Typography variant="body2" sx={{ fontFamily: "monospace" }}>{formatCount(cacheStatus?.projectionLag.token.lag ?? null)}</Typography> },
                { label: "Lifecycle lag", value: <Typography variant="body2" sx={{ fontFamily: "monospace" }}>{formatCount(cacheStatus?.projectionLag.txLifecycle.lag ?? null)}</Typography> },
                { label: "History envelope backfill", value: <Typography variant="body2" sx={{ fontFamily: "monospace" }}>{cacheStatus ? `${formatCount(cacheStatus.historyEnvelopeBackfill.pendingCount)} pending · scanned ${formatCount(cacheStatus.historyEnvelopeBackfill.lastBatchScanned)}` : "unavailable"}</Typography> },
              ]}
            />
          </Box>

          <KeyValueCard
            title="Raw transaction payload storage"
            description="Where raw payloads land when local persistence is active. This matters for replay, repair, and deep inspection workflows."
            status={{
              label: storageStatus?.rawTransactionPayloads.persistenceActive ? "active" : "inactive",
              color: storageStatus?.rawTransactionPayloads.persistenceActive ? "success" : "warning",
            }}
            rows={[
              { label: "Provider", value: <Typography variant="body2" sx={{ fontFamily: "monospace" }}>{storageStatus?.rawTransactionPayloads.provider ?? "unavailable"}</Typography> },
              { label: "Implemented", value: <Typography variant="body2">{storageStatus?.rawTransactionPayloads.providerImplemented ? "Yes" : "No"}</Typography> },
              { label: "Retention", value: <Typography variant="body2">{storageStatus?.rawTransactionPayloads.retentionPolicy ?? "unavailable"}</Typography> },
              { label: "Compression", value: <Typography variant="body2">{storageStatus?.rawTransactionPayloads.compression ?? "unavailable"}</Typography> },
              { label: "Database / collection", value: <Typography variant="body2" sx={{ fontFamily: "monospace", wordBreak: "break-all" }}>{storageStatus?.rawTransactionPayloads.location.database || "unavailable"}{storageStatus?.rawTransactionPayloads.location.collection ? ` / ${storageStatus.rawTransactionPayloads.location.collection}` : ""}</Typography> },
              { label: "Filesystem / bucket", value: <Typography variant="body2" sx={{ fontFamily: "monospace", wordBreak: "break-all" }}>{storageStatus?.rawTransactionPayloads.location.rootPath || storageStatus?.rawTransactionPayloads.location.bucket || "unavailable"}</Typography> },
            ]}
          />

          {storageStatus?.rawTransactionPayloads.notes?.length ? (
            <Alert severity="info">{storageStatus.rawTransactionPayloads.notes.join(" ")}</Alert>
          ) : null}

          <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", xl: "repeat(2, minmax(0, 1fr))" }, gap: 2 }}>
            <JsonPanel title="Additional cache detail" data={store.opsCache} />
            <JsonPanel title="Additional storage detail" data={store.opsStorage} />
          </Box>
        </Stack>
      )}
    </Box>
  );
});
