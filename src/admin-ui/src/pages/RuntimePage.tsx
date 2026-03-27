import { observer } from "mobx-react-lite";
import {
  Box,
  Typography,
  Alert,
  Button,
  Skeleton,
  IconButton,
  Tooltip,
  CircularProgress,
  Card,
  CardContent,
  Chip,
  Divider,
  MenuItem,
  Stack,
  TextField,
} from "@mui/material";
import RefreshOutlinedIcon from "@mui/icons-material/RefreshOutlined";
import { opsStore } from "@/stores/ops.store";
import { JsonPanel } from "@/components/JsonPanel";
import { ConfirmDialog } from "@/components/ConfirmDialog";
import { useState } from "react";

export const RuntimePage = observer(function RuntimePage() {
  const store = opsStore;
  const [applyOpen, setApplyOpen] = useState(false);
  const [resetOpen, setResetOpen] = useState(false);
  const policy = store.runtimeSources?.realtimePolicy;

  return (
    <Box>
      <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", mb: 3 }}>
        <Typography variant="h5" sx={{ fontWeight: 600, letterSpacing: "-0.02em" }}>
          Runtime / Ops
        </Typography>
        <Tooltip title="Refresh">
          <IconButton
            size="small"
            onClick={() => store.reload()}
            disabled={store.isLoading || store.refreshing}
            sx={{ color: "text.disabled" }}
          >
            {store.refreshing ? (
              <CircularProgress size={16} color="inherit" />
            ) : (
              <RefreshOutlinedIcon fontSize="small" />
            )}
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
        <Box sx={{ display: "flex", flexDirection: "column", gap: 2 }}>
          <Skeleton variant="rounded" height={56} />
          <Skeleton variant="rounded" height={56} />
          <Skeleton variant="rounded" height={56} />
        </Box>
      ) : (
        <Box sx={{ display: "flex", flexDirection: "column", gap: 2 }}>
          {policy && (
            <Card sx={{ borderRadius: 3 }}>
              <CardContent>
                <Stack spacing={2.5}>
                  <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 2 }}>
                    <Box>
                      <Typography variant="h6" sx={{ fontWeight: 600 }}>
                        Realtime Sources
                      </Typography>
                      <Typography variant="body2" color="text.secondary">
                        Minimal operator override for realtime primary source and Bitails transport.
                      </Typography>
                    </Box>
                    <Chip
                      size="small"
                      label={policy.overrideActive ? "Override Active" : "Static Policy"}
                      color={policy.overrideActive ? "warning" : "default"}
                      variant={policy.overrideActive ? "filled" : "outlined"}
                    />
                  </Box>

                  {policy.restartRequired && (
                    <Alert severity="warning">
                      Realtime policy overrides are persisted immediately, but a service restart is required for source selection changes to take full effect.
                    </Alert>
                  )}

                  <Box
                    sx={{
                      display: "grid",
                      gridTemplateColumns: { xs: "1fr", md: "repeat(3, minmax(0, 1fr))" },
                      gap: 2,
                    }}
                  >
                    <PolicyCard title="Static" value={policy.static} />
                    <PolicyCard title="Override" value={policy.override} emptyLabel="No override" />
                    <PolicyCard title="Effective" value={policy.effective} />
                  </Box>

                  <Divider />

                  <Box
                    sx={{
                      display: "grid",
                      gridTemplateColumns: { xs: "1fr", md: "repeat(2, minmax(0, 1fr))" },
                      gap: 2,
                    }}
                  >
                    <TextField
                      select
                      fullWidth
                      size="small"
                      label="Primary Realtime Source"
                      value={store.realtimePrimarySourceDraft}
                      onChange={(event) => store.setRealtimePrimarySource(event.target.value)}
                    >
                      {policy.allowedPrimarySources.map((source) => (
                        <MenuItem key={source} value={source}>
                          {source}
                        </MenuItem>
                      ))}
                    </TextField>

                    <TextField
                      select
                      fullWidth
                      size="small"
                      label="Bitails Transport"
                      value={store.bitailsTransportDraft}
                      onChange={(event) => store.setBitailsTransport(event.target.value)}
                    >
                      {policy.allowedBitailsTransports.map((transport) => (
                        <MenuItem key={transport} value={transport}>
                          {transport}
                        </MenuItem>
                      ))}
                    </TextField>
                  </Box>

                  <Box sx={{ display: "flex", flexWrap: "wrap", gap: 1.5 }}>
                    <Button
                      variant="contained"
                      onClick={() => setApplyOpen(true)}
                      disabled={!store.hasRealtimePolicyDraftChanges || store.savingRealtimePolicy}
                    >
                      {store.savingRealtimePolicy ? "Applying..." : "Apply Override"}
                    </Button>
                    <Button
                      variant="outlined"
                      color="inherit"
                      onClick={() => setResetOpen(true)}
                      disabled={!policy.overrideActive || store.resettingRealtimePolicy}
                    >
                      {store.resettingRealtimePolicy ? "Resetting..." : "Reset Override"}
                    </Button>
                  </Box>

                  <Typography variant="caption" color="text.secondary">
                    Updated by: {policy.updatedBy ?? "n/a"} {policy.updatedAt ? `at ${new Date(policy.updatedAt).toLocaleString()}` : ""}
                  </Typography>
                </Stack>
              </CardContent>
            </Card>
          )}

          <JsonPanel title="Cache Status (admin)" data={store.adminCacheStatus} />
          <JsonPanel title="Providers (ops)" data={store.providers} />
          <JsonPanel title="Cache Detail (ops)" data={store.opsCache} />
        </Box>
      )}

      <ConfirmDialog
        open={applyOpen}
        title="Apply realtime source override"
        message={`Set realtime primary source to "${store.realtimePrimarySourceDraft}" and Bitails transport to "${store.bitailsTransportDraft}"?`}
        confirmLabel="Apply"
        loading={store.savingRealtimePolicy}
        onConfirm={async () => {
          setApplyOpen(false);
          await store.applyRealtimePolicy();
        }}
        onCancel={() => setApplyOpen(false)}
      />

      <ConfirmDialog
        open={resetOpen}
        title="Reset realtime source override"
        message="Reset the persisted realtime override and fall back to static configuration?"
        confirmLabel="Reset"
        loading={store.resettingRealtimePolicy}
        onConfirm={async () => {
          setResetOpen(false);
          await store.resetRealtimePolicy();
        }}
        onCancel={() => setResetOpen(false)}
      />
    </Box>
  );
});

function PolicyCard({
  title,
  value,
  emptyLabel,
}: {
  title: string;
  value: { primaryRealtimeSource: string; bitailsTransport: string; fallbackSources: string[] } | null;
  emptyLabel?: string;
}) {
  return (
    <Card variant="outlined" sx={{ borderRadius: 2.5 }}>
      <CardContent sx={{ display: "flex", flexDirection: "column", gap: 1 }}>
        <Typography variant="subtitle2" sx={{ fontWeight: 600 }}>
          {title}
        </Typography>
        {value ? (
          <>
            <Typography variant="body2">Primary: {value.primaryRealtimeSource}</Typography>
            <Typography variant="body2">Bitails transport: {value.bitailsTransport}</Typography>
            <Typography variant="body2">
              Fallbacks: {value.fallbackSources.length > 0 ? value.fallbackSources.join(", ") : "none"}
            </Typography>
          </>
        ) : (
          <Typography variant="body2" color="text.secondary">
            {emptyLabel ?? "n/a"}
          </Typography>
        )}
      </CardContent>
    </Card>
  );
}
