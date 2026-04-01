import { observer } from "mobx-react-lite";
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  Chip,
  CircularProgress,
  IconButton,
  Skeleton,
  Stack,
  Tooltip,
  Typography,
  Divider,
} from "@mui/material";
import RefreshOutlinedIcon from "@mui/icons-material/RefreshOutlined";
import { JsonPanel } from "@/components/JsonPanel";
import { opsStore } from "@/stores/ops.store";
import type { ProviderStatusResponse } from "@/types/api";

function formatDateTime(value: string | null | undefined): string {
  if (!value) return "unavailable";
  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) return "unavailable";
  return new Intl.DateTimeFormat("en-GB", {
    dateStyle: "medium",
    timeStyle: "short",
  }).format(parsed);
}

function formatCount(value: number | null | undefined): string {
  if (value == null) return "unavailable";
  return new Intl.NumberFormat("en-GB").format(value);
}

function formatMaybeRateLimit(value: ProviderStatusResponse["rateLimitState"]): string {
  if (!value) return "unavailable";
  if (value.remaining == null) return value.scope ? `${value.scope}` : "configured";
  return value.resetAt ? `${value.remaining} remaining · resets ${formatDateTime(value.resetAt)}` : `${value.remaining} remaining`;
}

function StatusRow({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <Box sx={{ display: "flex", alignItems: "flex-start", gap: 2, py: 0.5 }}>
      <Typography variant="body2" sx={{ color: "text.disabled", minWidth: 136, flexShrink: 0 }}>
        {label}
      </Typography>
      <Box sx={{ flex: 1 }}>{value}</Box>
    </Box>
  );
}

function ProviderStatusCard({ provider }: { provider: ProviderStatusResponse }) {
  const isJungleBus = provider.provider === "junglebus";
  const activeCapabilities = Object.entries(provider.capabilities ?? {}).filter(([, cap]) => cap.active);

  return (
    <Card
      variant="outlined"
      sx={{
        borderRadius: 3,
        ...(isJungleBus ? { borderColor: "primary.main", boxShadow: "0 0 0 1px rgba(107, 127, 255, 0.15)" } : {}),
      }}
    >
      <CardContent sx={{ p: 3 }}>
        <Stack spacing={1.5}>
          <Box sx={{ display: "flex", justifyContent: "space-between", gap: 2, alignItems: "flex-start" }}>
            <Box>
              <Typography variant="h6" sx={{ fontWeight: 600 }}>
                {provider.provider}
              </Typography>
              <Typography variant="body2" sx={{ color: "text.secondary", mt: 0.5 }}>
                {provider.enabled ? "Enabled" : "Disabled"} · {provider.configured ? "Configured" : "Missing config"}
              </Typography>
            </Box>
            <Chip
              size="small"
              label={provider.degraded ? "degraded" : provider.healthy ? "healthy" : "unavailable"}
              color={provider.degraded ? "warning" : provider.healthy ? "success" : "default"}
              variant={provider.degraded || provider.healthy ? "filled" : "outlined"}
            />
          </Box>

          <Box sx={{ display: "flex", gap: 0.75, flexWrap: "wrap" }}>
            {provider.roles.map((role) => (
              <Chip key={role} size="small" label={role} variant="outlined" />
            ))}
            {provider.capabilities?.block_backfill?.active && (
              <Chip size="small" label="block sync active" color="primary" variant="filled" />
            )}
            {provider.capabilities?.realtime_ingest?.active && (
              <Chip size="small" label="realtime active" color="primary" variant="filled" />
            )}
            {provider.capabilities?.raw_tx_fetch?.active && (
              <Chip size="small" label="raw tx active" color="secondary" variant="filled" />
            )}
          </Box>

          <Divider />

          <StatusRow label="Last success" value={<Typography variant="body2" sx={{ fontFamily: "monospace" }}>{formatDateTime(provider.lastSuccessAt)}</Typography>} />
          <StatusRow label="Last error" value={<Typography variant="body2" sx={{ fontFamily: "monospace" }}>{formatDateTime(provider.lastErrorAt)}</Typography>} />
          <StatusRow
            label="Last error code"
            value={<Typography variant="body2" sx={{ fontFamily: "monospace" }}>{provider.lastErrorCode || "unavailable"}</Typography>}
          />
          <StatusRow
            label="Rate limit"
            value={<Typography variant="body2" sx={{ fontFamily: "monospace" }}>{formatMaybeRateLimit(provider.rateLimitState)}</Typography>}
          />
          {isJungleBus && (
            <>
              <StatusRow
                label="Local indexed height"
                value={<Typography variant="body2" sx={{ fontFamily: "monospace" }}>{formatCount(opsStore.syncStatus?.height ?? null)}</Typography>}
              />
              <StatusRow
                label="Observed JungleBus height"
                value={<Typography variant="body2" sx={{ fontFamily: "monospace" }}>{formatCount(provider.observedHeight ?? null)}</Typography>}
              />
              <StatusRow
                label="Lag"
                value={<Typography variant="body2" sx={{ fontFamily: "monospace" }}>{formatCount(provider.lagBlocks ?? null)}</Typography>}
              />
              <StatusRow
                label="Observed at"
                value={<Typography variant="body2" sx={{ fontFamily: "monospace" }}>{formatDateTime(provider.observedAt)}</Typography>}
              />
              <StatusRow
                label="Last control"
                value={<Typography variant="body2" sx={{ fontFamily: "monospace" }}>{formatDateTime(provider.lastControlMessageAt)}</Typography>}
              />
            </>
          )}

          {activeCapabilities.length > 0 && (
            <Box sx={{ display: "flex", gap: 0.75, flexWrap: "wrap" }}>
              {activeCapabilities.map(([capability]) => (
                <Chip key={capability} size="small" label={`${capability}: active`} color="success" variant="outlined" />
              ))}
            </Box>
          )}
        </Stack>
      </CardContent>
    </Card>
  );
}

export const RuntimePage = observer(function RuntimePage() {
  const store = opsStore;

  const jungleBus = store.providers?.find((provider) => provider.provider === "junglebus") ?? null;
  const activeProviders = store.providers ?? [];

  return (
    <Box>
      <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", mb: 3 }}>
        <Box>
          <Typography variant="h5" sx={{ fontWeight: 600, letterSpacing: "-0.02em" }}>
            Runtime / Ops
          </Typography>
          <Typography variant="body2" sx={{ color: "text.secondary", mt: 0.5 }}>
            Read-only runtime visibility for JungleBus health, local sync progress, and provider routing.
          </Typography>
        </Box>
        <Tooltip title="Refresh">
          <IconButton
            size="small"
            onClick={() => store.reload()}
            disabled={store.isLoading || store.refreshing}
            sx={{ color: "text.disabled" }}
          >
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
          <Skeleton variant="rounded" height={120} />
          <Skeleton variant="rounded" height={180} />
          <Skeleton variant="rounded" height={56} />
        </Stack>
      ) : (
        <Stack spacing={2}>
          <Card variant="outlined" sx={{ borderRadius: 3 }}>
            <CardContent sx={{ p: 3 }}>
              <Stack spacing={2}>
                <Box sx={{ display: "flex", justifyContent: "space-between", gap: 2, alignItems: "flex-start" }}>
                  <Box>
                    <Typography variant="h6" sx={{ fontWeight: 600, mb: 0.25 }}>
                      JungleBus block sync
                    </Typography>
                    <Typography variant="body2" sx={{ color: "text.secondary" }}>
                      Health card for the block sync path. Lag is shown when the backend surfaces it; otherwise the card stays honest and marks it unavailable.
                    </Typography>
                  </Box>
                  <Chip
                    size="small"
                    label={jungleBus?.degraded ? "degraded" : jungleBus?.healthy ? "healthy" : "unavailable"}
                    color={jungleBus?.degraded ? "warning" : jungleBus?.healthy ? "success" : "default"}
                    variant={jungleBus?.degraded || jungleBus?.healthy ? "filled" : "outlined"}
                  />
                </Box>

                {jungleBus ? (
                  <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", md: "repeat(2, minmax(0, 1fr))" }, gap: 1.5 }}>
                    <Box>
                      <StatusRow label="Enabled" value={<Typography variant="body2">{jungleBus.enabled ? "Yes" : "No"}</Typography>} />
                      <StatusRow label="Configured" value={<Typography variant="body2">{jungleBus.configured ? "Yes" : "No"}</Typography>} />
                      <StatusRow label="Last success" value={<Typography variant="body2" sx={{ fontFamily: "monospace" }}>{formatDateTime(jungleBus.lastSuccessAt)}</Typography>} />
                      <StatusRow label="Last error" value={<Typography variant="body2" sx={{ fontFamily: "monospace" }}>{formatDateTime(jungleBus.lastErrorAt)}</Typography>} />
                      <StatusRow label="Last error code" value={<Typography variant="body2" sx={{ fontFamily: "monospace" }}>{jungleBus.lastErrorCode || "unavailable"}</Typography>} />
                    </Box>
                    <Box>
                      <StatusRow label="Local indexed height" value={<Typography variant="body2" sx={{ fontFamily: "monospace" }}>{formatCount(store.syncStatus?.height ?? null)}</Typography>} />
                      <StatusRow label="Observed JungleBus height" value={<Typography variant="body2" sx={{ fontFamily: "monospace" }}>{formatCount(jungleBus.observedHeight ?? null)}</Typography>} />
                      <StatusRow label="Lag" value={<Typography variant="body2" sx={{ fontFamily: "monospace" }}>{formatCount(jungleBus.lagBlocks ?? null)}</Typography>} />
                      <StatusRow label="Observed at" value={<Typography variant="body2" sx={{ fontFamily: "monospace" }}>{formatDateTime(jungleBus.observedAt)}</Typography>} />
                      <StatusRow label="Last control" value={<Typography variant="body2" sx={{ fontFamily: "monospace" }}>{formatDateTime(jungleBus.lastControlMessageAt)}</Typography>} />
                    </Box>
                  </Box>
                ) : (
                  <Alert severity="warning">JungleBus provider status is unavailable.</Alert>
                )}

                {jungleBus?.rateLimitState && (
                  <Alert severity="info">
                    <Typography variant="body2" sx={{ fontWeight: 600, mb: 0.25 }}>
                      Rate limit
                    </Typography>
                    <Typography variant="body2" sx={{ fontFamily: "monospace" }}>
                      {formatMaybeRateLimit(jungleBus.rateLimitState)}
                    </Typography>
                  </Alert>
                )}
              </Stack>
            </CardContent>
          </Card>

          <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", xl: "repeat(2, minmax(0, 1fr))" }, gap: 2 }}>
            {activeProviders.map((provider) => (
              <ProviderStatusCard key={provider.provider} provider={provider} />
            ))}
          </Box>

          <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", xl: "repeat(2, minmax(0, 1fr))" }, gap: 2 }}>
            <JsonPanel title="Cache Status (admin)" data={store.adminCacheStatus} />
            <JsonPanel title="Cache Detail (ops)" data={store.opsCache} />
          </Box>

          <JsonPanel title="Storage Detail (ops)" data={store.opsStorage} />
        </Stack>
      )}
    </Box>
  );
});
