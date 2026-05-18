import {
  Alert,
  AlertTitle,
  Card,
  CardContent,
  CardHeader,
  Chip,
  LinearProgress,
  Snackbar,
  Stack,
  Typography,
} from "@mui/material";
import { observer } from "mobx-react-lite";
import { lazy, Suspense, useEffect, useMemo, useState } from "react";
import { AlertsStore } from "@/screens/alerts/alerts.store";
import type { IAdminClient } from "@/lib/admin/admin-client";
import type { P2pAlertEventDto } from "@/types/admin";

// S8-style route-level lazy boundary: DataGrid is heavy + only used
// on the history pane, so keep it off the shell + S7 first-paint.
const AlertHistoryGrid = lazy(() =>
  import("@/screens/alerts/AlertHistoryGrid").then((m) => ({
    default: m.AlertHistoryGrid,
  }))
);

const TOAST_MAX_IN_FLIGHT = 3;
const TOAST_AUTO_HIDE_MS = 8_000;

interface ToastEntry {
  key: string;
  alert: P2pAlertEventDto;
}

export const AlertsPage = observer(function AlertsPage({
  admin,
}: {
  admin: IAdminClient;
}) {
  const store = useMemo(() => new AlertsStore({ admin }), [admin]);
  useEffect(() => {
    void store.start();
    return () => store.dispose();
  }, [store]);

  const [toasts, setToasts] = useState<ToastEntry[]>([]);

  // Drain the delta queue on every MobX tick — the page owns the
  // toast lifecycle so the store stays UI-agnostic.
  useEffect(() => {
    const drain = store.consumeNewAlerts();
    if (drain.length === 0) return;
    setToasts((prev) => {
      const next = [
        ...drain.map((a) => ({ key: `${a.id}-${a.alertUnixMs}`, alert: a })),
        ...prev,
      ];
      return next.slice(0, TOAST_MAX_IN_FLIGHT);
    });
  }, [store, store.newSinceLastTick.length]);

  return (
    <Stack spacing={3}>
      <Stack direction="row" alignItems="baseline" spacing={2} flexWrap="wrap" useFlexGap>
        <Typography variant="h4">Alerts</Typography>
        <Chip label="S7" size="small" variant="outlined" />
        <Chip
          label={`${store.activeCount} active`}
          size="small"
          color={store.activeCount > 0 ? "warning" : "default"}
        />
      </Stack>

      {store.status === "loading" && <LinearProgress />}
      {store.error && (
        <Alert severity="error">
          Alert poll failed: {store.error}
        </Alert>
      )}

      <Card>
        <CardHeader title="Active" subheader="alerts within the last 30 min" />
        <CardContent>
          {store.activeAlerts.length === 0 ? (
            <Typography variant="body2" color="text.secondary">
              No active alerts.
            </Typography>
          ) : (
            <Stack spacing={1.5}>
              {store.activeAlerts.map((a) => (
                <ActiveAlertCard key={a.id} alert={a} />
              ))}
            </Stack>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader title="History" subheader="older + acknowledged events" />
        <CardContent>
          {store.historyAlerts.length === 0 ? (
            <Typography variant="body2" color="text.secondary">
              No history events.
            </Typography>
          ) : (
            <Suspense fallback={<LinearProgress />}>
              <AlertHistoryGrid alerts={store.historyAlerts} />
            </Suspense>
          )}
        </CardContent>
      </Card>

      {toasts.map((t) => (
        <Snackbar
          key={t.key}
          open
          autoHideDuration={TOAST_AUTO_HIDE_MS}
          onClose={() =>
            setToasts((prev) => prev.filter((p) => p.key !== t.key))
          }
          anchorOrigin={{ vertical: "bottom", horizontal: "right" }}
          data-testid={`alert-toast-${t.alert.id}`}
        >
          <Alert
            severity={severityForType(t.alert.type)}
            variant="filled"
            onClose={() =>
              setToasts((prev) => prev.filter((p) => p.key !== t.key))
            }
          >
            <AlertTitle>{t.alert.type}</AlertTitle>
            {t.alert.detail}
          </Alert>
        </Snackbar>
      ))}
    </Stack>
  );
});

function ActiveAlertCard({ alert }: { alert: P2pAlertEventDto }) {
  const severity = severityForType(alert.type);
  return (
    <Alert severity={severity} data-testid={`alert-active-${alert.id}`}>
      <AlertTitle>{alert.type}</AlertTitle>
      <Stack spacing={0.5}>
        <Typography variant="body2">{alert.detail}</Typography>
        <Stack direction="row" spacing={1} useFlexGap flexWrap="wrap">
          <Typography variant="caption" color="text.secondary">
            {new Date(alert.alertUnixMs).toUTCString().slice(5, 25)} UTC
          </Typography>
          {Object.entries(alert.context).map(([k, v]) => (
            <Chip key={k} size="small" label={`${k}=${v}`} variant="outlined" />
          ))}
        </Stack>
      </Stack>
    </Alert>
  );
}

function severityForType(type: string): "error" | "warning" | "info" {
  switch (type) {
    case "PoolSizeBelowThreshold":
    case "ReorgDepthExceeded":
      return "error";
    case "SourceFirstDropout":
    case "RelayBackRateBelowThreshold":
      return "warning";
    default:
      return "info";
  }
}

export const __testing = { severityForType };
