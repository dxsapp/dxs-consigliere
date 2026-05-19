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
import { observer } from "mobx-react-lite";
import { useEffect, useMemo } from "react";
import { SetupStore } from "@/screens/setup/setup.store";
import type { IAdminClient } from "@/lib/admin/admin-client";

/**
 * S10 — Setup. Read-only status panel that surfaces whether the
 * environment is set up, who the admin account is, and whether the
 * setup wizard is required (first-run scenario). Lifecycle owned by
 * `SetupStore` (S7-S12-audit M2).
 */
export const SetupPage = observer(function SetupPage({
  admin,
}: {
  admin: IAdminClient;
}) {
  const store = useMemo(() => new SetupStore({ admin }), [admin]);
  useEffect(() => {
    void store.start();
    return () => store.dispose();
  }, [store]);

  const data = store.data;

  return (
    <Stack spacing={3}>
      <Stack direction="row" alignItems="baseline" spacing={2} flexWrap="wrap" useFlexGap>
        <Typography variant="h4">Setup</Typography>
        <Chip label="S10" size="small" variant="outlined" />
      </Stack>

      {store.status === "loading" && <LinearProgress />}
      {store.error && <Alert severity="error">{store.error}</Alert>}

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
});

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
