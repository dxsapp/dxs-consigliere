import {
  Alert,
  Card,
  CardContent,
  CardHeader,
  Chip,
  Divider,
  LinearProgress,
  Stack,
  Typography,
} from "@mui/material";
import { useEffect, useState } from "react";
import type { IAdminClient } from "@/lib/admin/admin-client";
import type { SetupStatusResponse } from "@/types/admin";

/**
 * S10 — Setup. Read-only status panel that surfaces whether the
 * environment is set up, who the admin account is, and whether the
 * setup wizard is required (first-run scenario).
 *
 * The actual wizard form (POST /api/setup/complete) is a first-run
 * flow served before authenticated routes; the admin shell doesn't
 * need to embed it here — operators in production should hit the
 * `/setup` flow before they reach the admin UI.
 */
export function SetupPage({ admin }: { admin: IAdminClient }) {
  const [data, setData] = useState<SetupStatusResponse | null>(null);
  const [status, setStatus] = useState<"loading" | "ready" | "error">("loading");
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const ctl = new AbortController();
    admin
      .getSetupStatus(ctl.signal)
      .then((res) => {
        setData(res);
        setStatus("ready");
      })
      .catch((err) => {
        if (ctl.signal.aborted) return;
        setError(err instanceof Error ? err.message : "load failed");
        setStatus("error");
      });
    return () => ctl.abort();
  }, [admin]);

  return (
    <Stack spacing={3}>
      <Stack direction="row" alignItems="baseline" spacing={2} flexWrap="wrap" useFlexGap>
        <Typography variant="h4">Setup</Typography>
        <Chip label="S10" size="small" variant="outlined" />
      </Stack>

      {status === "loading" && <LinearProgress />}
      {error && <Alert severity="error">{error}</Alert>}

      {data && (
        <Card>
          <CardHeader title="Status" />
          <CardContent>
            <Stack divider={<Divider flexItem />} spacing={1}>
              <Row k="Setup required" v={yes(data.setupRequired)} />
              <Row k="Setup completed" v={yes(data.setupCompleted)} />
              <Row k="Admin enabled" v={yes(data.adminEnabled)} />
              <Row k="Admin username" v={data.adminUsername ?? "—"} />
            </Stack>
            {data.setupRequired && (
              <Alert severity="warning" sx={{ mt: 2 }}>
                The instance still reports <code>setupRequired</code>. Visit
                {" "}<code>/setup</code> on this host to complete the first-run wizard.
              </Alert>
            )}
          </CardContent>
        </Card>
      )}
    </Stack>
  );
}

function Row({ k, v }: { k: string; v: string }) {
  return (
    <Stack direction="row" justifyContent="space-between" alignItems="baseline" spacing={2}>
      <Typography variant="body2" color="text.secondary">{k}</Typography>
      <Typography variant="body2">{v}</Typography>
    </Stack>
  );
}

function yes(b: boolean): string {
  return b ? "yes" : "no";
}
