import { useMemo, useState } from "react";
import { observer } from "mobx-react-lite";
import {
  Alert,
  Box,
  Button,
  Card,
  CardActions,
  CardContent,
  Chip,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogContentText,
  DialogTitle,
  IconButton,
  Link,
  MenuItem,
  Skeleton,
  Stack,
  TextField,
  Tooltip,
  Typography,
} from "@mui/material";
import RefreshOutlinedIcon from "@mui/icons-material/RefreshOutlined";
import WarningAmberOutlinedIcon from "@mui/icons-material/WarningAmberOutlined";
import { providersStore } from "@/stores/providers.store";

function formatTs(ts: number): string {
  return new Intl.DateTimeFormat("en-GB", {
    dateStyle: "medium",
    timeStyle: "short",
  }).format(new Date(ts));
}

function ValueRow({ label, value }: { label: string; value: string }) {
  return (
    <Box sx={{ display: "flex", gap: 1, py: 0.35, alignItems: "baseline" }}>
      <Typography variant="caption" sx={{ color: "text.disabled", minWidth: 92, flexShrink: 0 }}>
        {label}
      </Typography>
      <Typography variant="body2" sx={{ fontFamily: "monospace", fontSize: "0.82rem" }}>
        {value || "—"}
      </Typography>
    </Box>
  );
}

function ConfigColumn({
  title,
  values,
  accent,
  emptyLabel,
}: {
  title: string;
  values: {
    realtimePrimaryProvider: string;
    restPrimaryProvider: string;
    bitailsTransport: string;
  } | null;
  accent?: boolean;
  emptyLabel?: string;
}) {
  return (
    <Card variant="outlined" sx={{ borderRadius: 2, ...(accent ? { borderColor: "primary.main" } : {}) }}>
      <CardContent sx={{ p: "14px 16px", "&:last-child": { pb: "14px" } }}>
        <Typography
          variant="overline"
          sx={{
            fontSize: "0.62rem",
            letterSpacing: "0.08em",
            color: accent ? "primary.light" : "text.secondary",
            display: "block",
            mb: values ? 1 : 0.5,
          }}
        >
          {title}
        </Typography>
        {values ? (
          <Box>
            <ValueRow label="Realtime" value={values.realtimePrimaryProvider} />
            <ValueRow label="REST" value={values.restPrimaryProvider} />
            <ValueRow label="Bitails" value={values.bitailsTransport} />
          </Box>
        ) : (
          <Typography variant="body2" sx={{ color: "text.disabled", fontStyle: "italic", fontSize: "0.82rem" }}>
            {emptyLabel ?? "—"}
          </Typography>
        )}
      </CardContent>
    </Card>
  );
}

function ConfirmDialog({
  open,
  title,
  body,
  confirmLabel,
  onCancel,
  onConfirm,
}: {
  open: boolean;
  title: string;
  body: string;
  confirmLabel: string;
  onCancel: () => void;
  onConfirm: () => void;
}) {
  return (
    <Dialog open={open} onClose={onCancel} maxWidth="sm" fullWidth>
      <DialogTitle sx={{ pb: 1 }}>{title}</DialogTitle>
      <DialogContent>
        <DialogContentText sx={{ fontSize: "0.875rem" }}>{body}</DialogContentText>
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2 }}>
        <Button onClick={onCancel} color="inherit">
          Cancel
        </Button>
        <Button onClick={onConfirm} variant="contained">
          {confirmLabel}
        </Button>
      </DialogActions>
    </Dialog>
  );
}

export const ProvidersPage = observer(function ProvidersPage() {
  const store = providersStore;
  const [applyOpen, setApplyOpen] = useState(false);
  const [resetOpen, setResetOpen] = useState(false);
  const config = store.config;
  const draft = store.draft;

  const effectiveDiff = useMemo(() => {
    if (!config || !draft) return [];
    const e = config.effective;
    const diffs: string[] = [];

    // Selector fields — show from→to
    if (draft.realtimePrimaryProvider !== e.realtimePrimaryProvider)
      diffs.push(`Realtime: ${e.realtimePrimaryProvider} → ${draft.realtimePrimaryProvider}`);
    if (draft.restPrimaryProvider !== e.restPrimaryProvider)
      diffs.push(`REST: ${e.restPrimaryProvider} → ${draft.restPrimaryProvider}`);
    if (draft.bitailsTransport !== e.bitailsTransport)
      diffs.push(`Bitails transport: ${e.bitailsTransport} → ${draft.bitailsTransport}`);

    // Bitails connection fields — show label only (no value leak for secrets)
    if (draft.bitails.apiKey !== (e.bitails?.apiKey ?? ""))
      diffs.push("Bitails API key: changed");
    if (draft.bitails.baseUrl !== (e.bitails?.baseUrl ?? ""))
      diffs.push(`Bitails REST URL: ${draft.bitails.baseUrl || "cleared"}`);
    if (draft.bitails.websocketBaseUrl !== (e.bitails?.websocketBaseUrl ?? ""))
      diffs.push(`Bitails WS URL: ${draft.bitails.websocketBaseUrl || "cleared"}`);
    if (draft.bitails.zmqTxUrl !== (e.bitails?.zmqTxUrl ?? ""))
      diffs.push(`Bitails ZMQ tx URL: ${draft.bitails.zmqTxUrl || "cleared"}`);
    if (draft.bitails.zmqBlockUrl !== (e.bitails?.zmqBlockUrl ?? ""))
      diffs.push(`Bitails ZMQ block URL: ${draft.bitails.zmqBlockUrl || "cleared"}`);

    // WhatsOnChain fields
    if (draft.whatsonchain.apiKey !== (e.whatsonchain?.apiKey ?? ""))
      diffs.push("WhatsOnChain API key: changed");
    if (draft.whatsonchain.baseUrl !== (e.whatsonchain?.baseUrl ?? ""))
      diffs.push(`WhatsOnChain base URL: ${draft.whatsonchain.baseUrl || "cleared"}`);

    // JungleBus fields
    if (draft.junglebus.baseUrl !== (e.junglebus?.baseUrl ?? ""))
      diffs.push(`JungleBus base URL: ${draft.junglebus.baseUrl || "cleared"}`);
    if (draft.junglebus.mempoolSubscriptionId !== (e.junglebus?.mempoolSubscriptionId ?? ""))
      diffs.push("JungleBus mempool subscription ID: changed");
    if (draft.junglebus.blockSubscriptionId !== (e.junglebus?.blockSubscriptionId ?? ""))
      diffs.push("JungleBus block subscription ID: changed");

    return diffs;
  }, [config, draft]);

  return (
    <Box>
      <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", mb: 3 }}>
        <Box>
          <Typography variant="h5" sx={{ fontWeight: 600, letterSpacing: "-0.02em" }}>
            Providers
          </Typography>
          <Typography variant="body2" sx={{ color: "text.secondary", mt: 0.5 }}>
            Ecosystem provider catalog, recommended defaults, and minimal operator configuration for realtime, raw transaction fetch, and REST fallback.
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
        <Alert severity="error" sx={{ mb: 2 }} action={<Button color="inherit" size="small" onClick={() => store.reload()}>Retry</Button>}>
          {store.error}
        </Alert>
      )}

      {store.isLoading && !store.refreshing ? (
        <Stack spacing={2}>
          <Skeleton variant="rounded" height={110} />
          <Skeleton variant="rounded" height={220} />
          <Skeleton variant="rounded" height={320} />
        </Stack>
      ) : (
        <Stack spacing={2}>
          {config && (
            <Card variant="outlined" sx={{ borderRadius: 3 }}>
              <CardContent sx={{ p: 3 }}>
                <Stack spacing={2}>
                  <Box sx={{ display: "flex", justifyContent: "space-between", gap: 2, alignItems: "flex-start" }}>
                    <Box>
                      <Typography variant="h6" sx={{ fontWeight: 600, mb: 0.25 }}>Recommended setup</Typography>
                      <Typography variant="body2" sx={{ color: "text.secondary" }}>
                        Start with Bitails websocket for managed realtime ingest without requiring an API key on day one, use JungleBus / GorillaPool for practical raw transaction fetches, and keep WhatsOnChain as the easy REST fallback/onboarding path. ZMQ remains an advanced infrastructure option.
                      </Typography>
                    </Box>
                    <Chip size="small" label={config.overrideActive ? "Override Active" : "Static Only"} color={config.overrideActive ? "warning" : "default"} variant={config.overrideActive ? "filled" : "outlined"} />
                  </Box>

                  <Box sx={{ display: "flex", gap: 1, flexWrap: "wrap" }}>
                    <Chip label={`Realtime: ${store.snapshot?.recommendations.realtimePrimaryProvider ?? "bitails"}`} color="primary" variant="outlined" />
                    <Chip label={`Raw tx: ${store.snapshot?.recommendations.rawTxFetchProvider ?? "junglebus"}`} color="secondary" variant="outlined" />
                    <Chip label={`REST: ${store.snapshot?.recommendations.restPrimaryProvider ?? "whatsonchain"}`} color="primary" variant="outlined" />
                  </Box>
                </Stack>
              </CardContent>
            </Card>
          )}

          {config && (
            <Card variant="outlined" sx={{ borderRadius: 3 }}>
              <CardContent sx={{ p: 3 }}>
                <Stack spacing={2.5}>
                  <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", md: "repeat(3, minmax(0, 1fr))" }, gap: 2 }}>
                    <ConfigColumn title="Static" values={config.static} />
                    <ConfigColumn title="Override" values={config.override} emptyLabel="No override set" />
                    <ConfigColumn title="Effective" values={config.effective} accent />
                  </Box>

                  {config.restartRequired && (
                    <Alert severity="warning" icon={<WarningAmberOutlinedIcon />}>
                      <Typography variant="body2" sx={{ fontWeight: 600, mb: 0.25 }}>Service restart required</Typography>
                      <Typography variant="body2">
                        Provider overrides are persisted, but runtime source selection and client wiring apply fully only after restart.
                      </Typography>
                    </Alert>
                  )}

                  {config.overrideActive && (
                    <Typography variant="caption" sx={{ color: "text.disabled" }}>
                      Override set by <Box component="span" sx={{ fontFamily: "monospace" }}>{config.updatedBy ?? "unknown"}</Box>
                      {config.updatedAt != null && ` · ${formatTs(config.updatedAt)}`}
                    </Typography>
                  )}
                </Stack>
              </CardContent>
            </Card>
          )}

          {store.snapshot && (
            <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", xl: "repeat(2, minmax(0, 1fr))" }, gap: 2 }}>
              {store.snapshot.providers.map((provider) => (
                <Card key={provider.providerId} variant="outlined" sx={{ borderRadius: 3 }}>
                  <CardContent sx={{ p: 3 }}>
                    <Stack spacing={1.5}>
                      <Box sx={{ display: "flex", justifyContent: "space-between", gap: 2, alignItems: "flex-start" }}>
                        <Box>
                          <Typography variant="h6" sx={{ fontWeight: 600 }}>{provider.displayName}</Typography>
                          <Typography variant="body2" sx={{ color: "text.secondary", mt: 0.5 }}>{provider.description}</Typography>
                        </Box>
                        <Chip size="small" label={provider.status.replaceAll("_", " ")} color={provider.status === "active" ? "primary" : provider.status === "missing_requirements" ? "warning" : "default"} />
                      </Box>

                      <Box sx={{ display: "flex", gap: 0.75, flexWrap: "wrap" }}>
                        {provider.roles.map((role) => <Chip key={role} size="small" label={role} variant="outlined" />)}
                        {provider.recommendedFor.map((role) => <Chip key={`recommended-${role}`} size="small" label={`recommended ${role}`} color="success" variant="outlined" />)}
                        {provider.activeFor.map((role) => <Chip key={`active-${role}`} size="small" label={`active ${role}`} color="primary" variant="filled" />)}
                      </Box>

                      {provider.missingRequirements.length > 0 && (
                        <Alert severity="warning">
                          <Typography variant="body2" sx={{ fontWeight: 600, mb: 0.25 }}>Missing requirements</Typography>
                          <Typography variant="body2">{provider.missingRequirements.join(", ")}</Typography>
                        </Alert>
                      )}

                      <Stack direction="row" spacing={2} flexWrap="wrap" useFlexGap>
                        {provider.helpLinks.map((link) => (
                          <Link key={`${provider.providerId}-${link.url}`} href={link.url} target="_blank" rel="noreferrer" underline="hover" variant="body2">
                            {link.label}
                          </Link>
                        ))}
                      </Stack>
                    </Stack>
                  </CardContent>
                </Card>
              ))}
            </Box>
          )}

          {config && draft && (
            <Card variant="outlined" sx={{ borderRadius: 3 }}>
              <CardContent sx={{ p: 3 }}>
                <Stack spacing={2.5}>
                  <Box>
                    <Typography variant="h6" sx={{ fontWeight: 600, mb: 0.25 }}>Configure providers</Typography>
                    <Typography variant="body2" sx={{ color: "text.secondary" }}>
                      Keep this surface narrow: choose the primary realtime provider, Bitails transport, primary REST provider, and only the connection fields needed to get started.
                    </Typography>
                  </Box>

                  <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", md: "repeat(3, minmax(0, 1fr))" }, gap: 2 }}>
                    <TextField select size="small" label="Realtime provider" value={draft.realtimePrimaryProvider} onChange={(e) => store.setRealtimePrimaryProvider(e.target.value)}>
                      {config.allowedRealtimePrimaryProviders.map((option) => (
                        <MenuItem key={option} value={option}>{option}</MenuItem>
                      ))}
                    </TextField>
                    <TextField select size="small" label="Bitails transport" value={draft.bitailsTransport} onChange={(e) => store.setBitailsTransport(e.target.value)}>
                      {config.allowedBitailsTransports.map((option) => (
                        <MenuItem key={option} value={option}>{option}</MenuItem>
                      ))}
                    </TextField>
                    <TextField select size="small" label="REST provider" value={draft.restPrimaryProvider} onChange={(e) => store.setRestPrimaryProvider(e.target.value)}>
                      {config.allowedRestPrimaryProviders.map((option) => (
                        <MenuItem key={option} value={option}>{option}</MenuItem>
                      ))}
                    </TextField>
                  </Box>

                  <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", lg: "repeat(3, minmax(0, 1fr))" }, gap: 2 }}>
                    <Card variant="outlined" sx={{ borderRadius: 2 }}>
                      <CardContent>
                        <Stack spacing={1.5}>
                          <Typography variant="subtitle2" sx={{ fontWeight: 600 }}>Bitails</Typography>
                          <Typography variant="caption" sx={{ color: "text.secondary" }}>
                            Websocket is the default-on onboarding path. Leave the API key blank for first-run websocket use, then add one later if you need paid or higher-limit provider usage.
                          </Typography>
                          <TextField size="small" label="API key" value={draft.bitails.apiKey} onChange={(e) => store.setBitailsField("apiKey", e.target.value)} />
                          <TextField size="small" label="REST base URL" value={draft.bitails.baseUrl} onChange={(e) => store.setBitailsField("baseUrl", e.target.value)} />
                          <TextField size="small" label="Websocket base URL" value={draft.bitails.websocketBaseUrl} onChange={(e) => store.setBitailsField("websocketBaseUrl", e.target.value)} />
                          <TextField size="small" label="ZMQ tx URL" value={draft.bitails.zmqTxUrl} onChange={(e) => store.setBitailsField("zmqTxUrl", e.target.value)} />
                          <TextField size="small" label="ZMQ block URL" value={draft.bitails.zmqBlockUrl} onChange={(e) => store.setBitailsField("zmqBlockUrl", e.target.value)} />
                        </Stack>
                      </CardContent>
                    </Card>

                    <Card variant="outlined" sx={{ borderRadius: 2 }}>
                      <CardContent>
                        <Stack spacing={1.5}>
                          <Typography variant="subtitle2" sx={{ fontWeight: 600 }}>WhatsOnChain</Typography>
                          <TextField size="small" label="API key" value={draft.whatsonchain.apiKey} onChange={(e) => store.setWhatsonchainField("apiKey", e.target.value)} />
                          <TextField size="small" label="Base URL" value={draft.whatsonchain.baseUrl} onChange={(e) => store.setWhatsonchainField("baseUrl", e.target.value)} />
                        </Stack>
                      </CardContent>
                    </Card>

                    <Card variant="outlined" sx={{ borderRadius: 2 }}>
                      <CardContent>
                        <Stack spacing={1.5}>
                          <Typography variant="subtitle2" sx={{ fontWeight: 600 }}>JungleBus</Typography>
                          <TextField size="small" label="Base URL" value={draft.junglebus.baseUrl} onChange={(e) => store.setJunglebusField("baseUrl", e.target.value)} />
                          <TextField size="small" label="Mempool subscription ID" value={draft.junglebus.mempoolSubscriptionId} onChange={(e) => store.setJunglebusField("mempoolSubscriptionId", e.target.value)} />
                          <TextField size="small" label="Block subscription ID" value={draft.junglebus.blockSubscriptionId} onChange={(e) => store.setJunglebusField("blockSubscriptionId", e.target.value)} />
                        </Stack>
                      </CardContent>
                    </Card>
                  </Box>

                  {effectiveDiff.length > 0 && (
                    <Alert severity="info">
                      <Typography variant="body2" sx={{ fontWeight: 600, mb: 0.25 }}>Pending changes</Typography>
                      <Typography variant="body2">{effectiveDiff.join(" · ")}</Typography>
                    </Alert>
                  )}
                </Stack>
              </CardContent>
              <CardActions sx={{ px: 3, pb: 3, pt: 0, display: "flex", gap: 1.5, flexWrap: "wrap" }}>
                <Tooltip title={!store.hasDraftChanges ? "No changes from current configuration" : ""}>
                  <span>
                    <Button
                      variant="contained"
                      disabled={!store.hasDraftChanges || store.saving || store.resetting}
                      startIcon={store.saving ? <CircularProgress size={14} color="inherit" /> : undefined}
                      onClick={() => setApplyOpen(true)}
                    >
                      {store.saving ? "Saving…" : "Apply configuration"}
                    </Button>
                  </span>
                </Tooltip>
                <Tooltip title={!config.overrideActive ? "No active override to reset" : ""}>
                  <span>
                    <Button
                      variant="outlined"
                      color="inherit"
                      disabled={!config.overrideActive || store.saving || store.resetting}
                      startIcon={store.resetting ? <CircularProgress size={14} color="inherit" /> : undefined}
                      onClick={() => setResetOpen(true)}
                    >
                      {store.resetting ? "Resetting…" : "Reset to static"}
                    </Button>
                  </span>
                </Tooltip>
              </CardActions>
            </Card>
          )}
        </Stack>
      )}

      <ConfirmDialog
        open={applyOpen}
        title="Apply provider configuration"
        body="The provider override will be persisted outside static configuration. Runtime source changes require a restart before all background tasks and client wiring use the new setup."
        confirmLabel="Apply"
        onCancel={() => setApplyOpen(false)}
        onConfirm={() => {
          setApplyOpen(false);
          void store.apply();
        }}
      />

      <ConfirmDialog
        open={resetOpen}
        title="Reset provider configuration"
        body="The persisted provider override will be removed. Effective provider selection will return to static configuration on the next refresh and restart cycle."
        confirmLabel="Reset"
        onCancel={() => setResetOpen(false)}
        onConfirm={() => {
          setResetOpen(false);
          void store.reset();
        }}
      />
    </Box>
  );
});
