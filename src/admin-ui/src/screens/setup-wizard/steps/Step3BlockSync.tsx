import { Alert, Stack, TextField, Typography } from "@mui/material";
import { observer } from "mobx-react-lite";
import type { SetupWizardStore } from "@/screens/setup-wizard/setup-wizard.store";

export const Step3BlockSync = observer(function Step3BlockSync({
  store,
}: {
  store: SetupWizardStore;
}) {
  const errors = new Map(store.errorsForStep(3).map((e) => [e.field, e.message]));
  return (
    <Stack spacing={2} data-testid="setup-step-3">
      <Typography variant="body2" color="text.secondary">
        Consigliere syncs blocks via the JungleBus subscription
        service. Both values below are required by the backend
        validator.
      </Typography>
      <TextField
        label="JungleBus base URL"
        value={store.blockSync.baseUrl}
        onChange={(e) => store.setBlockSyncField("baseUrl", e.target.value)}
        error={errors.has("blockSync.baseUrl")}
        helperText={errors.get("blockSync.baseUrl") ?? "Defaults to the public JungleBus endpoint"}
        fullWidth
        size="small"
      />
      <TextField
        label="Block subscription ID"
        value={store.blockSync.blockSubscriptionId}
        onChange={(e) => store.setBlockSyncField("blockSubscriptionId", e.target.value)}
        error={errors.has("blockSync.blockSubscriptionId")}
        helperText={
          errors.get("blockSync.blockSubscriptionId") ??
          "Obtain a subscription ID from JungleBus before going to production. Any non-empty string passes for a smoke install."
        }
        fullWidth
        size="small"
      />
      <Alert severity="info">
        The subscription ID is forwarded to both
        <code> Junglebus.BlockSubscriptionId </code>
        and the dedicated <code>BlockSync</code> block on submit, matching the
        backend wiring in <code>SetupWizardService</code>.
      </Alert>
    </Stack>
  );
});
