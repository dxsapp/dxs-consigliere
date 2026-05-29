import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  CardHeader,
  LinearProgress,
  Stack,
} from "@mui/material";
import { observer } from "mobx-react-lite";
import { useEffect, useMemo } from "react";
import { Navigate, useNavigate } from "react-router-dom";
import { Step1AdminAccess } from "@/screens/setup-wizard/steps/Step1AdminAccess";
import { SetupWizardStore } from "@/screens/setup-wizard/setup-wizard.store";
import { LOGIN_PATH } from "@/app/routes";
import type { IAdminClient } from "@/lib/admin/admin-client";
import type { AuthStore } from "@/stores/root";

/**
 * wave simplified-first-run-wizard S2 — public first-run setup wizard.
 *
 * Mounted at `/setup` outside `AuthGuard`. Pulls
 * `GET /api/setup/options` on mount, asks the operator for the ONE
 * required thing — an admin account — posts `POST /api/setup/complete`,
 * then redirects to `/login` (operator signs in with the credentials
 * they just chose).
 *
 * The node runs on the built-in P2P thin node, so no provider config
 * and no JungleBus block subscription are required to finish setup.
 * Providers + history sync are optional and configurable later under
 * Settings.
 *
 * Already-completed installs are bounced to `/login` on mount; no
 * setup form rendered.
 */
export const SetupWizardPage = observer(function SetupWizardPage({
  admin,
  auth,
}: {
  admin: IAdminClient;
  auth: AuthStore;
}) {
  const store = useMemo(() => new SetupWizardStore({ admin }), [admin]);
  const navigate = useNavigate();

  useEffect(() => {
    void store.start();
    return () => store.dispose();
  }, [store]);

  // Post-submit: flip auth.setupRequired off using the response
  // payload BEFORE the network re-hydrate. Two reasons:
  //  - The wizard already knows the new state authoritatively from
  //    POST /api/setup/complete — no need to round-trip again to
  //    learn the same thing.
  //  - A transient /me failure during the subsequent hydrate would
  //    otherwise leave `setupRequired === true` and bounce the
  //    operator straight back to /setup via AuthGuard (wave-A2
  //    S0-audit M1).
  // The hook order is stable (this effect MUST come before any
  // conditional return) per react-hooks/rules-of-hooks.
  useEffect(() => {
    if (store.status !== "submitted" || !store.submittedStatus) return;
    let cancelled = false;
    auth.applySetupStatus(store.submittedStatus);
    void (async () => {
      // Best-effort re-hydrate to pick up session-side state
      // (cookie freshness, admin username, etc.). Failures are
      // already covered by applySetupStatus above — we just
      // navigate regardless.
      try {
        await auth.hydrate();
      } catch {
        /* swallow — setupRequired is already cleared */
      }
      if (cancelled) return;
      navigate(LOGIN_PATH, { replace: true });
    })();
    return () => {
      cancelled = true;
    };
  }, [store.status, store.submittedStatus, auth, navigate]);

  // Once setup is already done (e.g. operator refreshed the page
  // post-submit, or visited /setup on a hydrated install), bounce.
  // wave-A2 S0-audit L1: while the initial getSetupOptions() is in
  // flight we render a neutral loader instead of the full wizard
  // chrome — otherwise an already-completed install briefly shows
  // the form before the redirect lands.
  if (
    store.options &&
    store.options.status.setupCompleted &&
    store.status !== "submitted"
  ) {
    return <Navigate to={LOGIN_PATH} replace />;
  }
  if (store.status === "loading" && !store.options) {
    return (
      <Box
        sx={{
          minHeight: "100vh",
          display: "grid",
          placeItems: "center",
          bgcolor: "background.default",
        }}
        data-testid="setup-wizard-loading"
      >
        <LinearProgress sx={{ width: 240 }} />
      </Box>
    );
  }

  return (
    <Box sx={{ minHeight: "100vh", py: 6, px: { xs: 2, sm: 4 }, bgcolor: "background.default" }}>
      <Card sx={{ maxWidth: 560, mx: "auto" }}>
        <CardHeader
          title="Consigliere — create your admin account"
          subheader="That's the only step. The node runs on the built-in P2P thin node — no subscriptions or third-party providers needed. Providers and history sync are optional under Settings."
        />
        <CardContent>
          {/* Initial loading is handled by the outer skeleton
              above (wave-A2 S0-audit L1). Once status flips to
              "submitting", show inline progress instead. */}
          {store.status === "submitting" && <LinearProgress />}

          {store.status === "error" && (
            <Alert
              severity="error"
              action={
                <Button color="inherit" size="small" onClick={() => void store.start()}>
                  Retry
                </Button>
              }
            >
              {store.error}
            </Alert>
          )}

          {(store.status === "ready" ||
            store.status === "submitting" ||
            store.status === "submitted") && (
            <Stack spacing={3}>
              <Step1AdminAccess store={store} />

              {store.error && store.status === "ready" && (
                <Alert severity="error">{store.error}</Alert>
              )}

              {store.status === "submitted" && (
                <Alert severity="success">
                  Setup complete — redirecting to sign-in…
                </Alert>
              )}

              <Stack direction="row" justifyContent="flex-end" alignItems="center">
                <Button
                  variant="contained"
                  color="primary"
                  disabled={!store.canSubmit || store.status === "submitting"}
                  onClick={() => void store.submit()}
                >
                  {store.status === "submitting" ? "Creating account…" : "Create account"}
                </Button>
              </Stack>
            </Stack>
          )}
        </CardContent>
      </Card>
    </Box>
  );
});
