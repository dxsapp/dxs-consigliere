import { Stack, TextField, Typography } from "@mui/material";
import { observer } from "mobx-react-lite";
import type { SetupWizardStore } from "@/screens/setup-wizard/setup-wizard.store";

export const Step1AdminAccess = observer(function Step1AdminAccess({
  store,
}: {
  store: SetupWizardStore;
}) {
  const errors = new Map(store.errorsForStep(1).map((e) => [e.field, e.message]));
  return (
    <Stack spacing={2} data-testid="setup-step-1">
      <Typography variant="body2" color="text.secondary">
        Create the admin operator account. Cookie-mode authentication
        signs the operator in immediately after submit.
      </Typography>
      <TextField
        label="Operator name"
        value={store.admin.username}
        onChange={(e) => store.setAdminField("username", e.target.value)}
        error={errors.has("admin.username")}
        helperText={errors.get("admin.username") ?? "At least 3 characters"}
        autoFocus
        fullWidth
        size="small"
      />
      <TextField
        label="Password"
        type="password"
        value={store.admin.password}
        onChange={(e) => store.setAdminField("password", e.target.value)}
        error={errors.has("admin.password")}
        helperText={errors.get("admin.password") ?? "At least 8 characters"}
        fullWidth
        size="small"
      />
      <TextField
        label="Confirm password"
        type="password"
        value={store.admin.confirmPassword}
        onChange={(e) => store.setAdminField("confirmPassword", e.target.value)}
        error={errors.has("admin.confirmPassword")}
        helperText={errors.get("admin.confirmPassword") ?? "Must match the password above"}
        fullWidth
        size="small"
      />
    </Stack>
  );
});
