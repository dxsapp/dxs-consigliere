import {
  Alert,
  Card,
  CardContent,
  CardHeader,
  Divider,
  Stack,
  Typography,
} from "@mui/material";
import { observer } from "mobx-react-lite";
import type { SetupWizardStore } from "@/screens/setup-wizard/setup-wizard.store";

export const Step4Review = observer(function Step4Review({
  store,
}: {
  store: SetupWizardStore;
}) {
  const errors = store.errorsForStep(4);
  return (
    <Stack spacing={2} data-testid="setup-step-4">
      <Typography variant="body2" color="text.secondary">
        Review the assembled request before submitting. The
        backend rejects any combination that violates its own
        validation; client-side checks shown below mirror those
        rules.
      </Typography>

      {errors.length > 0 && (
        <Alert severity="warning">
          {errors.length} issue{errors.length === 1 ? "" : "s"} remain in earlier steps; go back to fix them before submitting.
        </Alert>
      )}

      <Card variant="outlined">
        <CardHeader title="Admin" />
        <CardContent>
          <Stack spacing={0.5}>
            <Row k="Username" v={store.admin.username || "—"} />
            <Row k="Password" v={store.admin.password ? "•".repeat(store.admin.password.length) : "—"} />
            <Row k="Auth enabled" v="yes" />
          </Stack>
        </CardContent>
      </Card>

      <Card variant="outlined">
        <CardHeader title="Providers" />
        <CardContent>
          <Stack spacing={0.5} divider={<Divider flexItem />}>
            <Row k="Realtime primary" v={store.providers.realtimePrimaryProvider} />
            <Row k="RawTx primary" v={store.providers.rawTxPrimaryProvider} />
            <Row k="REST fallback" v={store.providers.restFallbackProvider} />
            <Row k="Bitails transport" v={store.providers.bitailsTransport} />
            <Row k="Bitails base URL" v={store.providers.bitailsBaseUrl || "—"} mono />
            <Row k="Bitails websocket URL" v={store.providers.bitailsWebsocketBaseUrl || "—"} mono />
            <Row k="WhatsOnChain base URL" v={store.providers.whatsonchainBaseUrl || "—"} mono />
            <Row k="JungleBus base URL" v={store.providers.junglebusBaseUrl || "—"} mono />
          </Stack>
        </CardContent>
      </Card>

      <Card variant="outlined">
        <CardHeader title="Block sync" />
        <CardContent>
          <Stack spacing={0.5} divider={<Divider flexItem />}>
            <Row k="Base URL" v={store.blockSync.baseUrl || "—"} mono />
            <Row k="Subscription ID" v={store.blockSync.blockSubscriptionId || "—"} mono />
          </Stack>
        </CardContent>
      </Card>
    </Stack>
  );
});

function Row({ k, v, mono = false }: { k: string; v: string; mono?: boolean }) {
  return (
    <Stack direction="row" justifyContent="space-between" alignItems="baseline" spacing={2}>
      <Typography variant="body2" color="text.secondary">{k}</Typography>
      <Typography
        variant={mono ? "code" : "body2"}
        sx={{ wordBreak: "break-all", textAlign: "right" }}
      >
        {v}
      </Typography>
    </Stack>
  );
}
