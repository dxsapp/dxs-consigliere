import {
  Divider,
  MenuItem,
  Stack,
  TextField,
  Typography,
} from "@mui/material";
import { observer } from "mobx-react-lite";
import type { SetupWizardStore } from "@/screens/setup-wizard/setup-wizard.store";

// thin-node-primary-source wave S5 — human-readable labels for the
// routable provider values the backend returns. The thin node (`p2p`)
// is the recommended realtime + rawTx primary and needs no URL/key.
const PROVIDER_LABELS: Record<string, string> = {
  p2p: "Thin node (P2P)",
  bitails: "Bitails",
  junglebus: "JungleBus",
  whatsonchain: "WhatsOnChain",
  node: "Bitcoin SV node (RPC)",
};

function providerLabel(value: string): string {
  return PROVIDER_LABELS[value.toLowerCase()] ?? value;
}

export const Step2Providers = observer(function Step2Providers({
  store,
}: {
  store: SetupWizardStore;
}) {
  const errors = new Map(store.errorsForStep(2).map((e) => [e.field, e.message]));
  const allowed = store.options?.allowed;
  if (!allowed) return null;

  return (
    <Stack spacing={2} data-testid="setup-step-2">
      <Typography variant="body2" color="text.secondary">
        Consigliere indexes BSV straight from the P2P network by default:
        the thin node (P2P) is the recommended realtime + rawTx primary
        and needs no URL or API key. The external providers below are
        configured as measurable fallbacks; JungleBus also serves
        historical block sync (configured in the next step). Per-provider
        URLs are pre-filled from public defaults.
      </Typography>

      <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
        <TextField
          select
          label="Realtime primary (recommended: Thin node)"
          value={store.providers.realtimePrimaryProvider}
          onChange={(e) => store.setProvidersField("realtimePrimaryProvider", e.target.value)}
          error={errors.has("providers.realtimePrimaryProvider")}
          helperText={errors.get("providers.realtimePrimaryProvider") ?? " "}
          fullWidth
          size="small"
        >
          {allowed.realtimePrimaryProviders.map((p) => (
            <MenuItem key={p} value={p}>{providerLabel(p)}</MenuItem>
          ))}
        </TextField>
        <TextField
          select
          label="RawTx primary (recommended: Thin node)"
          value={store.providers.rawTxPrimaryProvider}
          onChange={(e) => store.setProvidersField("rawTxPrimaryProvider", e.target.value)}
          error={errors.has("providers.rawTxPrimaryProvider")}
          helperText={errors.get("providers.rawTxPrimaryProvider") ?? " "}
          fullWidth
          size="small"
        >
          {allowed.rawTxPrimaryProviders.map((p) => (
            <MenuItem key={p} value={p}>{providerLabel(p)}</MenuItem>
          ))}
        </TextField>
        <TextField
          select
          label="REST fallback"
          value={store.providers.restFallbackProvider}
          onChange={(e) => store.setProvidersField("restFallbackProvider", e.target.value)}
          error={errors.has("providers.restFallbackProvider")}
          helperText={errors.get("providers.restFallbackProvider") ?? " "}
          fullWidth
          size="small"
        >
          {allowed.restFallbackProviders.map((p) => (
            <MenuItem key={p} value={p}>{providerLabel(p)}</MenuItem>
          ))}
        </TextField>
      </Stack>

      <Divider />

      <Typography variant="body2" color="text.secondary">
        Fallback providers — used when the thin node cannot serve a
        request (e.g. confirmed/historical rawTx the peers do not relay).
        They also run as redundant realtime observers so a cold-start
        peer pool never stalls ingest.
      </Typography>

      <Typography variant="overline" color="text.secondary">Bitails (fallback)</Typography>
      <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
        <TextField
          select
          label="Transport"
          value={store.providers.bitailsTransport}
          onChange={(e) => store.setProvidersField("bitailsTransport", e.target.value)}
          error={errors.has("providers.bitailsTransport")}
          helperText={errors.get("providers.bitailsTransport") ?? " "}
          size="small"
          sx={{ minWidth: 160 }}
        >
          {allowed.bitailsTransports.map((t) => (
            <MenuItem key={t} value={t}>{t}</MenuItem>
          ))}
        </TextField>
        <TextField
          label="Base URL"
          value={store.providers.bitailsBaseUrl}
          onChange={(e) => store.setProvidersField("bitailsBaseUrl", e.target.value)}
          error={errors.has("providers.bitailsBaseUrl")}
          helperText={errors.get("providers.bitailsBaseUrl") ?? " "}
          fullWidth
          size="small"
        />
        <TextField
          label="Websocket URL"
          value={store.providers.bitailsWebsocketBaseUrl}
          onChange={(e) => store.setProvidersField("bitailsWebsocketBaseUrl", e.target.value)}
          error={errors.has("providers.bitailsWebsocketBaseUrl")}
          helperText={errors.get("providers.bitailsWebsocketBaseUrl") ?? " "}
          fullWidth
          size="small"
        />
      </Stack>
      <TextField
        label="Bitails API key (optional)"
        value={store.providers.bitailsApiKey}
        onChange={(e) => store.setProvidersField("bitailsApiKey", e.target.value)}
        fullWidth
        size="small"
      />

      <Divider />

      <Typography variant="overline" color="text.secondary">WhatsOnChain (fallback)</Typography>
      <TextField
        label="Base URL"
        value={store.providers.whatsonchainBaseUrl}
        onChange={(e) => store.setProvidersField("whatsonchainBaseUrl", e.target.value)}
        error={errors.has("providers.whatsonchainBaseUrl")}
        helperText={errors.get("providers.whatsonchainBaseUrl") ?? " "}
        fullWidth
        size="small"
      />
      <TextField
        label="API key (optional)"
        value={store.providers.whatsonchainApiKey}
        onChange={(e) => store.setProvidersField("whatsonchainApiKey", e.target.value)}
        fullWidth
        size="small"
      />

      <Divider />

      <Typography variant="overline" color="text.secondary">
        JungleBus (fallback + historical block sync)
      </Typography>
      <TextField
        label="Base URL"
        value={store.providers.junglebusBaseUrl}
        onChange={(e) => store.setProvidersField("junglebusBaseUrl", e.target.value)}
        error={errors.has("providers.junglebusBaseUrl")}
        helperText={errors.get("providers.junglebusBaseUrl") ?? " "}
        fullWidth
        size="small"
      />
      <TextField
        label="Mempool subscription ID (optional)"
        value={store.providers.junglebusMempoolSubscriptionId}
        onChange={(e) => store.setProvidersField("junglebusMempoolSubscriptionId", e.target.value)}
        fullWidth
        size="small"
      />
    </Stack>
  );
});
