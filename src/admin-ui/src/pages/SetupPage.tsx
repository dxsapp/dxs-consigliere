import { useEffect, useMemo, useState } from "react";
import { Navigate, useNavigate } from "react-router-dom";
import { observer } from "mobx-react-lite";
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  Checkbox,
  FormControlLabel,
  MenuItem,
  Stack,
  Step,
  StepLabel,
  Stepper,
  TextField,
  Typography,
} from "@mui/material";
import { setupStore } from "@/stores/setup.store";
import { authStore } from "@/stores/auth.store";

const STEPS = ["Admin access", "Raw transaction source", "REST fallback", "Realtime source", "Review"];

export const SetupPage = observer(function SetupPage() {
  const navigate = useNavigate();
  const [step, setStep] = useState(0);
  const [confirmPassword, setConfirmPassword] = useState("");

  useEffect(() => {
    void setupStore.ensureLoaded();
  }, []);

  const options = setupStore.options;
  const draft = setupStore.draft;

  const currentStepValid = useMemo(() => {
    if (!draft) return false;
    if (step === 0) {
      if (!draft.admin.enabled) return true;
      return Boolean(draft.admin.username && draft.admin.password && draft.admin.password === confirmPassword);
    }
    if (step === 1) {
      if (!draft.providers.rawTxPrimaryProvider) return false;
      if (draft.providers.rawTxPrimaryProvider === "junglebus") {
        return Boolean(draft.providers.junglebus.baseUrl);
      }
      if (draft.providers.rawTxPrimaryProvider === "whatsonchain") {
        return Boolean(draft.providers.whatsonchain.baseUrl);
      }
      return Boolean(draft.providers.bitails.baseUrl);
    }
    if (step === 2) {
      if (!draft.providers.restFallbackProvider) return false;
      if (draft.providers.restFallbackProvider === "whatsonchain") {
        return Boolean(draft.providers.whatsonchain.baseUrl);
      }
      return Boolean(draft.providers.bitails.baseUrl);
    }
    if (step === 3) {
      if (!draft.providers.realtimePrimaryProvider) return false;
      if (draft.providers.realtimePrimaryProvider === "bitails") {
        if (draft.providers.bitailsTransport === "zmq") {
          return Boolean(draft.providers.bitails.zmqTxUrl || draft.providers.bitails.zmqBlockUrl);
        }
        return Boolean(draft.providers.bitails.websocketBaseUrl || draft.providers.bitails.baseUrl);
      }
      return Boolean(draft.providers.junglebus.baseUrl && draft.providers.junglebus.mempoolSubscriptionId);
    }
    return true;
  }, [confirmPassword, draft, step]);

  if (options?.status.setupCompleted && !authStore.setupRequired) {
    return <Navigate to={options.status.adminEnabled ? "/login" : "/"} replace />;
  }

  const handleComplete = async () => {
    const status = await setupStore.complete();
    if (!status) return;
    await authStore.refresh();
    navigate(status.adminEnabled ? "/login" : "/", { replace: true });
  };

  return (
    <Box sx={{ minHeight: "100vh", display: "flex", alignItems: "center", justifyContent: "center", p: 3 }}>
      <Card sx={{ width: "min(980px, 100%)" }}>
        <CardContent sx={{ p: 4 }}>
          <Stack spacing={3}>
            <Box>
              <Typography variant="h4" sx={{ fontWeight: 700, letterSpacing: "-0.03em", color: "primary.light" }}>
                Consigliere setup
              </Typography>
              <Typography variant="body2" sx={{ color: "text.secondary", mt: 0.75 }}>
                First-run configuration by capability: admin access, raw transaction fetch, REST fallback, and realtime ingest.
              </Typography>
            </Box>

            {setupStore.error && <Alert severity="error">{setupStore.error}</Alert>}

            <Stepper activeStep={step} alternativeLabel>
              {STEPS.map((label) => (
                <Step key={label}>
                  <StepLabel>{label}</StepLabel>
                </Step>
              ))}
            </Stepper>

            {!draft || !options ? (
              <Alert severity="info">Loading setup options…</Alert>
            ) : (
              <Stack spacing={3}>
                {step === 0 && (
                  <Stack spacing={2}>
                    <Typography variant="h6">Protect the admin shell</Typography>
                    <Typography variant="body2" sx={{ color: "text.secondary" }}>
                      Enable login only if this instance needs operator protection. If disabled, the admin shell stays open inside the trusted deployment.
                    </Typography>
                    <FormControlLabel
                      control={<Checkbox checked={draft.admin.enabled} onChange={(e) => setupStore.setAdminEnabled(e.target.checked)} />}
                      label="Enable admin login"
                    />
                    {draft.admin.enabled && (
                      <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", md: "repeat(3, minmax(0, 1fr))" }, gap: 2 }}>
                        <TextField label="Username" value={draft.admin.username} onChange={(e) => setupStore.setAdminUsername(e.target.value)} />
                        <TextField label="Password" type="password" value={draft.admin.password} onChange={(e) => setupStore.setAdminPassword(e.target.value)} />
                        <TextField label="Confirm password" type="password" value={confirmPassword} onChange={(e) => setConfirmPassword(e.target.value)} error={Boolean(confirmPassword) && draft.admin.password !== confirmPassword} helperText={Boolean(confirmPassword) && draft.admin.password !== confirmPassword ? "Passwords must match" : " "} />
                      </Box>
                    )}
                  </Stack>
                )}

                {step === 1 && (
                  <Stack spacing={2}>
                    <Typography variant="h6">Choose raw transaction source</Typography>
                    <Typography variant="body2" sx={{ color: "text.secondary" }}>
                      Consigliere uses this provider to fetch full raw transaction payloads by txid. JungleBus / GorillaPool is the recommended practical default.
                    </Typography>
                    <TextField select label="Raw transaction provider" value={draft.providers.rawTxPrimaryProvider} onChange={(e) => setupStore.setRawTxProvider(e.target.value)}>
                      {options.allowed.rawTxPrimaryProviders.map((provider) => (
                        <MenuItem key={provider} value={provider}>{provider}</MenuItem>
                      ))}
                    </TextField>
                    {draft.providers.rawTxPrimaryProvider === "junglebus" && (
                      <TextField label="JungleBus base URL" value={draft.providers.junglebus.baseUrl} onChange={(e) => setupStore.setJunglebusField("baseUrl", e.target.value)} helperText="Default GorillaPool transaction-get endpoint path." />
                    )}
                    {draft.providers.rawTxPrimaryProvider === "whatsonchain" && (
                      <TextField label="WhatsOnChain base URL" value={draft.providers.whatsonchain.baseUrl} onChange={(e) => setupStore.setWhatsonchainField("baseUrl", e.target.value)} helperText="Simple fallback path with well-known API surface." />
                    )}
                    {draft.providers.rawTxPrimaryProvider === "bitails" && (
                      <TextField label="Bitails REST base URL" value={draft.providers.bitails.baseUrl} onChange={(e) => setupStore.setBitailsField("baseUrl", e.target.value)} helperText="Optional alternative raw-tx path through Bitails REST." />
                    )}
                  </Stack>
                )}

                {step === 2 && (
                  <Stack spacing={2}>
                    <Typography variant="h6">Choose REST fallback</Typography>
                    <Typography variant="body2" sx={{ color: "text.secondary" }}>
                      This provider stays available for simple HTTP fallback paths. WhatsOnChain is the recommended starter default.
                    </Typography>
                    <TextField select label="REST fallback provider" value={draft.providers.restFallbackProvider} onChange={(e) => setupStore.setRestFallbackProvider(e.target.value)}>
                      {options.allowed.restFallbackProviders.map((provider) => (
                        <MenuItem key={provider} value={provider}>{provider}</MenuItem>
                      ))}
                    </TextField>
                    {draft.providers.restFallbackProvider === "whatsonchain" ? (
                      <Stack spacing={2}>
                        <TextField label="WhatsOnChain base URL" value={draft.providers.whatsonchain.baseUrl} onChange={(e) => setupStore.setWhatsonchainField("baseUrl", e.target.value)} />
                        <TextField label="WhatsOnChain API key (optional)" value={draft.providers.whatsonchain.apiKey} onChange={(e) => setupStore.setWhatsonchainField("apiKey", e.target.value)} />
                      </Stack>
                    ) : (
                      <Stack spacing={2}>
                        <TextField label="Bitails REST base URL" value={draft.providers.bitails.baseUrl} onChange={(e) => setupStore.setBitailsField("baseUrl", e.target.value)} />
                        <TextField label="Bitails API key (optional)" value={draft.providers.bitails.apiKey} onChange={(e) => setupStore.setBitailsField("apiKey", e.target.value)} helperText="Optional for first-run websocket onboarding; useful later for paid or higher-limit usage." />
                      </Stack>
                    )}
                  </Stack>
                )}

                {step === 3 && (
                  <Stack spacing={2}>
                    <Typography variant="h6">Choose realtime source</Typography>
                    <Typography variant="body2" sx={{ color: "text.secondary" }}>
                      Bitails websocket is the recommended first-run realtime path. JungleBus remains an advanced option for operators who already have subscription IDs.
                    </Typography>
                    <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", md: "1fr 1fr" }, gap: 2 }}>
                      <TextField select label="Realtime provider" value={draft.providers.realtimePrimaryProvider} onChange={(e) => setupStore.setRealtimeProvider(e.target.value)}>
                        {options.allowed.realtimePrimaryProviders.map((provider) => (
                          <MenuItem key={provider} value={provider}>{provider}</MenuItem>
                        ))}
                      </TextField>
                      {draft.providers.realtimePrimaryProvider === "bitails" && (
                        <TextField select label="Bitails transport" value={draft.providers.bitailsTransport} onChange={(e) => setupStore.setBitailsTransport(e.target.value)}>
                          {options.allowed.bitailsTransports.map((transport) => (
                            <MenuItem key={transport} value={transport}>{transport}</MenuItem>
                          ))}
                        </TextField>
                      )}
                    </Box>

                    {draft.providers.realtimePrimaryProvider === "bitails" && draft.providers.bitailsTransport === "websocket" && (
                      <Stack spacing={2}>
                        <TextField label="Bitails websocket URL" value={draft.providers.bitails.websocketBaseUrl} onChange={(e) => setupStore.setBitailsField("websocketBaseUrl", e.target.value)} />
                        <TextField label="Bitails API key (optional)" value={draft.providers.bitails.apiKey} onChange={(e) => setupStore.setBitailsField("apiKey", e.target.value)} />
                      </Stack>
                    )}

                    {draft.providers.realtimePrimaryProvider === "bitails" && draft.providers.bitailsTransport === "zmq" && (
                      <Stack spacing={2}>
                        <TextField label="Bitails ZMQ tx URL" value={draft.providers.bitails.zmqTxUrl} onChange={(e) => setupStore.setBitailsField("zmqTxUrl", e.target.value)} />
                        <TextField label="Bitails ZMQ block URL" value={draft.providers.bitails.zmqBlockUrl} onChange={(e) => setupStore.setBitailsField("zmqBlockUrl", e.target.value)} />
                      </Stack>
                    )}

                    {draft.providers.realtimePrimaryProvider === "junglebus" && (
                      <Stack spacing={2}>
                        <TextField label="JungleBus base URL" value={draft.providers.junglebus.baseUrl} onChange={(e) => setupStore.setJunglebusField("baseUrl", e.target.value)} />
                        <TextField label="Mempool subscription ID" value={draft.providers.junglebus.mempoolSubscriptionId} onChange={(e) => setupStore.setJunglebusField("mempoolSubscriptionId", e.target.value)} />
                        <TextField label="Block subscription ID" value={draft.providers.junglebus.blockSubscriptionId} onChange={(e) => setupStore.setJunglebusField("blockSubscriptionId", e.target.value)} />
                      </Stack>
                    )}
                  </Stack>
                )}

                {step === 4 && (
                  <Stack spacing={2}>
                    <Typography variant="h6">Review setup</Typography>
                    <Alert severity="info">The chosen provider configuration is persisted immediately. Runtime wiring applies fully after service restart.</Alert>
                    <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", md: "repeat(2, minmax(0, 1fr))" }, gap: 2 }}>
                      <Card variant="outlined"><CardContent><Typography variant="overline">Admin</Typography><Typography variant="body2">{draft.admin.enabled ? `Enabled (${draft.admin.username})` : "Disabled"}</Typography></CardContent></Card>
                      <Card variant="outlined"><CardContent><Typography variant="overline">Raw tx</Typography><Typography variant="body2">{draft.providers.rawTxPrimaryProvider}</Typography></CardContent></Card>
                      <Card variant="outlined"><CardContent><Typography variant="overline">REST fallback</Typography><Typography variant="body2">{draft.providers.restFallbackProvider}</Typography></CardContent></Card>
                      <Card variant="outlined"><CardContent><Typography variant="overline">Realtime</Typography><Typography variant="body2">{draft.providers.realtimePrimaryProvider}{draft.providers.realtimePrimaryProvider === "bitails" ? ` · ${draft.providers.bitailsTransport}` : ""}</Typography></CardContent></Card>
                    </Box>
                  </Stack>
                )}

                <Box sx={{ display: "flex", justifyContent: "space-between", gap: 2 }}>
                  <Button color="inherit" disabled={step === 0 || setupStore.saving} onClick={() => setStep((current) => Math.max(0, current - 1))}>
                    Back
                  </Button>
                  <Box sx={{ display: "flex", gap: 1.5 }}>
                    {step < STEPS.length - 1 ? (
                      <Button variant="contained" disabled={!currentStepValid || setupStore.saving} onClick={() => setStep((current) => Math.min(STEPS.length - 1, current + 1))}>
                        Next
                      </Button>
                    ) : (
                      <Button variant="contained" disabled={setupStore.saving} onClick={handleComplete}>
                        {setupStore.saving ? "Saving…" : "Save setup"}
                      </Button>
                    )}
                  </Box>
                </Box>
              </Stack>
            )}
          </Stack>
        </CardContent>
      </Card>
    </Box>
  );
});
