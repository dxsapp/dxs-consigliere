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
import type {
  JungleBusChainTipAssuranceResponse,
  ProviderStatusResponse,
  ValidationRepairStatusResponse,
} from "@/types/api";

function formatDateTime(value: string | number | null | undefined): string {
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
  const activeCapabilities = Object.entries(provider.capabilities ?? {}).filter(([, cap]) => cap.active);

  return (
    <Card
      variant="outlined"
      sx={{
        borderRadius: 3,
        ...(provider.provider === "junglebus" ? { borderColor: "primary.main", boxShadow: "0 0 0 1px rgba(107, 127, 255, 0.15)" } : {}),
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

function getAssuranceChip(response: JungleBusChainTipAssuranceResponse | null) {
  switch (response?.state) {
    case "healthy":
      return { label: "healthy", color: "success" as const };
    case "catching_up":
      return { label: "catching up", color: "info" as const };
    case "stalled_control_flow":
      return { label: "stalled control flow", color: "error" as const };
    case "stalled_local_progress":
      return { label: "stalled local progress", color: "warning" as const };
    default:
      return { label: "unavailable", color: "default" as const };
  }
}

function getValidationRepairChip(response: ValidationRepairStatusResponse | null) {
  if (!response) return { label: "unavailable", color: "default" as const };
  if (response.blockedCount > 0 || response.failedCount > 0) {
    return { label: "degraded", color: "warning" as const };
  }
  if (response.pendingCount > 0 || response.runningCount > 0) {
    return { label: "busy", color: "info" as const };
  }
  return { label: "idle", color: "success" as const };
}

export const RuntimePage = observer(function RuntimePage() {
  const store = opsStore;

  const jungleBus = store.providers?.find((provider) => provider.provider === "junglebus") ?? null;
  const activeProviders = store.providers ?? [];
  const jungleBusBlockSync = store.jungleBusBlockSync;
  const jungleBusAssurance = store.jungleBusChainTipAssurance;
  const validationRepairs = store.validationRepairs;
  const assuranceChip = getAssuranceChip(jungleBusAssurance);
  const validationRepairChip = getValidationRepairChip(validationRepairs);

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
                      Operational lag and scheduler health for the JungleBus-backed block sync path.
                    </Typography>
                  </Box>
                  <Chip
                    size="small"
                    label={jungleBusBlockSync?.degraded ? "degraded" : jungleBusBlockSync?.healthy ? "healthy" : "unavailable"}
                    color={jungleBusBlockSync?.degraded ? "warning" : jungleBusBlockSync?.healthy ? "success" : "default"}
                    variant={jungleBusBlockSync?.degraded || jungleBusBlockSync?.healthy ? "filled" : "outlined"}
                  />
                </Box>

                {jungleBusBlockSync ? (
                  <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", md: "repeat(2, minmax(0, 1fr))" }, gap: 1.5 }}>
                    <Box>
                      <StatusRow label="Primary" value={<Typography variant="body2">{jungleBusBlockSync.primary ? "Yes" : "No"}</Typography>} />
                      <StatusRow label="Configured" value={<Typography variant="body2">{jungleBusBlockSync.configured ? "Yes" : "No"}</Typography>} />
                      <StatusRow label="Unavailable reason" value={<Typography variant="body2" sx={{ fontFamily: "monospace" }}>{jungleBusBlockSync.unavailableReason || "none"}</Typography>} />
                      <StatusRow label="Base URL" value={<Typography variant="body2" sx={{ fontFamily: "monospace", wordBreak: "break-all" }}>{jungleBusBlockSync.baseUrl || "unavailable"}</Typography>} />
                      <StatusRow label="Last error" value={<Typography variant="body2" sx={{ fontFamily: "monospace" }}>{jungleBusBlockSync.lastError || "unavailable"}</Typography>} />
                    </Box>
                    <Box>
                      <StatusRow label="Local indexed height" value={<Typography variant="body2" sx={{ fontFamily: "monospace" }}>{formatCount(jungleBusBlockSync.highestKnownLocalBlockHeight ?? store.syncStatus?.height ?? null)}</Typography>} />
                      <StatusRow label="Observed JungleBus height" value={<Typography variant="body2" sx={{ fontFamily: "monospace" }}>{formatCount(jungleBusBlockSync.lastObservedBlockHeight ?? null)}</Typography>} />
                      <StatusRow label="Lag" value={<Typography variant="body2" sx={{ fontFamily: "monospace" }}>{formatCount(jungleBusBlockSync.lagBlocks ?? null)}</Typography>} />
                      <StatusRow label="Last control" value={<Typography variant="body2" sx={{ fontFamily: "monospace" }}>{formatDateTime(jungleBusBlockSync.lastControlMessageAt)}</Typography>} />
                      <StatusRow label="Last processed" value={<Typography variant="body2" sx={{ fontFamily: "monospace" }}>{formatDateTime(jungleBusBlockSync.lastProcessedAt)}</Typography>} />
                    </Box>
                  </Box>
                ) : (
                  <Alert severity="warning">JungleBus block-sync status is unavailable.</Alert>
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

          <Card variant="outlined" sx={{ borderRadius: 3 }}>
            <CardContent sx={{ p: 3 }}>
              <Stack spacing={2}>
                <Box sx={{ display: "flex", justifyContent: "space-between", gap: 2, alignItems: "flex-start" }}>
                  <Box>
                    <Typography variant="h6" sx={{ fontWeight: 600, mb: 0.25 }}>
                      JungleBus chain-tip assurance
                    </Typography>
                    <Typography variant="body2" sx={{ color: "text.secondary" }}>
                      Distinguishes plain lag from confidence in the JungleBus-first tip signal. Single-source assurance is surfaced explicitly when no secondary cross-check exists.
                    </Typography>
                  </Box>
                  <Chip
                    size="small"
                    label={assuranceChip.label}
                    color={assuranceChip.color}
                    variant={assuranceChip.color === "default" ? "outlined" : "filled"}
                  />
                </Box>

                {jungleBusAssurance ? (
                  <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", md: "repeat(2, minmax(0, 1fr))" }, gap: 1.5 }}>
                    <Box>
                      <StatusRow label="Assurance mode" value={<Typography variant="body2" sx={{ fontFamily: "monospace" }}>{jungleBusAssurance.assuranceMode}</Typography>} />
                      <StatusRow label="Single-source" value={<Typography variant="body2">{jungleBusAssurance.singleSourceAssurance ? "Yes" : "No"}</Typography>} />
                      <StatusRow label="Secondary cross-check" value={<Typography variant="body2">{jungleBusAssurance.secondaryCrossCheckAvailable ? "Available" : "Unavailable"}</Typography>} />
                      <StatusRow label="Control stalled" value={<Typography variant="body2">{jungleBusAssurance.controlFlowStalled ? "Yes" : "No"}</Typography>} />
                      <StatusRow label="Local progress stalled" value={<Typography variant="body2">{jungleBusAssurance.localProgressStalled ? "Yes" : "No"}</Typography>} />
                    </Box>
                    <Box>
                      <StatusRow label="Last observed movement" value={<Typography variant="body2" sx={{ fontFamily: "monospace" }}>{formatDateTime(jungleBusAssurance.lastObservedMovementAt)}</Typography>} />
                      <StatusRow label="Observed movement height" value={<Typography variant="body2" sx={{ fontFamily: "monospace" }}>{formatCount(jungleBusAssurance.lastObservedMovementHeight)}</Typography>} />
                      <StatusRow label="Last local progress" value={<Typography variant="body2" sx={{ fontFamily: "monospace" }}>{formatDateTime(jungleBusAssurance.lastLocalProgressAt)}</Typography>} />
                      <StatusRow label="Local progress height" value={<Typography variant="body2" sx={{ fontFamily: "monospace" }}>{formatCount(jungleBusAssurance.lastLocalProgressHeight)}</Typography>} />
                      <StatusRow label="Lag" value={<Typography variant="body2" sx={{ fontFamily: "monospace" }}>{formatCount(jungleBusAssurance.lagBlocks)}</Typography>} />
                    </Box>
                  </Box>
                ) : (
                  <Alert severity="warning">JungleBus chain-tip assurance is unavailable.</Alert>
                )}

                {jungleBusAssurance?.singleSourceAssurance && (
                  <Alert severity="warning">
                    <Typography variant="body2" sx={{ fontWeight: 600, mb: 0.25 }}>
                      Single-source assurance
                    </Typography>
                    <Typography variant="body2">
                      {jungleBusAssurance.note || "No secondary chain-tip cross-check is active in JungleBus-first mode."}
                    </Typography>
                  </Alert>
                )}

                {jungleBusAssurance?.state === "stalled_control_flow" && (
                  <Alert severity="error">
                    Control flow looks stale. No fresh JungleBus control message has arrived within the configured assurance window.
                  </Alert>
                )}

                {jungleBusAssurance?.state === "stalled_local_progress" && (
                  <Alert severity="warning">
                    JungleBus tip is still moving, but local indexed progress has stalled beyond the configured local-progress window.
                  </Alert>
                )}
              </Stack>
            </CardContent>
          </Card>

          <Card variant="outlined" sx={{ borderRadius: 3 }}>
            <CardContent sx={{ p: 3 }}>
              <Stack spacing={2}>
                <Box sx={{ display: "flex", justifyContent: "space-between", gap: 2, alignItems: "flex-start" }}>
                  <Box>
                    <Typography variant="h6" sx={{ fontWeight: 600, mb: 0.25 }}>
                      Validation dependency repair
                    </Typography>
                    <Typography variant="body2" sx={{ color: "text.secondary" }}>
                      Durable queue for unresolved STAS or DSTAS lineage dependencies. Consigliere remains the validation authority; providers only supply missing data.
                    </Typography>
                  </Box>
                  <Chip
                    size="small"
                    label={validationRepairChip.label}
                    color={validationRepairChip.color}
                    variant={validationRepairChip.color === "default" ? "outlined" : "filled"}
                  />
                </Box>

                {validationRepairs ? (
                  <>
                    <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", md: "repeat(2, minmax(0, 1fr))" }, gap: 1.5 }}>
                      <Box>
                        <StatusRow label="Pending" value={<Typography variant="body2" sx={{ fontFamily: "monospace" }}>{formatCount(validationRepairs.pendingCount)}</Typography>} />
                        <StatusRow label="Running" value={<Typography variant="body2" sx={{ fontFamily: "monospace" }}>{formatCount(validationRepairs.runningCount)}</Typography>} />
                        <StatusRow label="Failed" value={<Typography variant="body2" sx={{ fontFamily: "monospace" }}>{formatCount(validationRepairs.failedCount)}</Typography>} />
                        <StatusRow label="Blocked" value={<Typography variant="body2" sx={{ fontFamily: "monospace" }}>{formatCount(validationRepairs.blockedCount)}</Typography>} />
                      </Box>
                      <Box>
                        <StatusRow label="Resolved" value={<Typography variant="body2" sx={{ fontFamily: "monospace" }}>{formatCount(validationRepairs.resolvedCount)}</Typography>} />
                        <StatusRow label="Total items" value={<Typography variant="body2" sx={{ fontFamily: "monospace" }}>{formatCount(validationRepairs.totalCount)}</Typography>} />
                        <StatusRow label="Oldest unresolved" value={<Typography variant="body2" sx={{ fontFamily: "monospace" }}>{validationRepairs.oldestUnresolvedAgeSeconds == null ? "none" : `${formatCount(validationRepairs.oldestUnresolvedAgeSeconds)} s`}</Typography>} />
                        <StatusRow label="Created at" value={<Typography variant="body2" sx={{ fontFamily: "monospace" }}>{formatDateTime(validationRepairs.oldestUnresolvedCreatedAt)}</Typography>} />
                      </Box>
                    </Box>

                    {validationRepairs.blockedCount > 0 && (
                      <Alert severity="error">
                        Some validation repairs are blocked. Inspect recent items and their last error before trusting unresolved token lineage verdicts.
                      </Alert>
                    )}

                    {validationRepairs.failedCount > 0 && validationRepairs.blockedCount === 0 && (
                      <Alert severity="warning">
                        Some validation repairs exhausted retries. Resolution remains manual until new dependency data arrives.
                      </Alert>
                    )}

                    {validationRepairs.items.length > 0 && (
                      <Stack spacing={1}>
                        {validationRepairs.items.slice(0, 5).map((item) => (
                          <Box
                            key={`${item.entityType}:${item.entityId}`}
                            sx={{
                              border: "1px solid",
                              borderColor: "divider",
                              borderRadius: 2,
                              p: 1.5,
                              display: "grid",
                              gridTemplateColumns: { xs: "1fr", md: "minmax(0, 1.5fr) minmax(0, 1fr)" },
                              gap: 1.5,
                            }}
                          >
                            <Box>
                              <Typography variant="body2" sx={{ fontFamily: "monospace", wordBreak: "break-all" }}>
                                {item.entityId}
                              </Typography>
                              <Typography variant="caption" sx={{ color: "text.secondary" }}>
                                {item.state} · attempts {item.attemptCount}
                              </Typography>
                            </Box>
                            <Box>
                              <Typography variant="caption" sx={{ display: "block", color: "text.secondary" }}>
                                Missing deps: {item.missingDependencies.length} · reasons: {item.reasons.join(", ") || "n/a"}
                              </Typography>
                              <Typography variant="caption" sx={{ display: "block", color: "text.secondary" }}>
                                Last error: {item.lastError || "none"}
                              </Typography>
                              <Typography variant="caption" sx={{ display: "block", color: "text.secondary" }}>
                                Stop reason: {item.lastStopReason || "unavailable"} · fetched {formatCount(item.lastFetchCount)} · visited {formatCount(item.lastVisitedCount)} · depth {formatCount(item.lastTraversalDepth)}
                              </Typography>
                            </Box>
                          </Box>
                        ))}
                      </Stack>
                    )}
                  </>
                ) : (
                  <Alert severity="warning">Validation repair status is unavailable.</Alert>
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
