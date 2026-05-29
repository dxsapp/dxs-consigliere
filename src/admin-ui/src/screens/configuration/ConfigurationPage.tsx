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
import { ConfigurationStore } from "@/screens/configuration/configuration.store";
import type { IAdminClient } from "@/lib/admin/admin-client";

/**
 * S10 — Configuration screen. Read-only sectioned view of the
 * effective provider config (Bitails / WhatsOnChain / JungleBus).
 * No `tune` affordance per the design brief — operators change
 * config out-of-band via env / config files and reload. Lifecycle
 * owned by `ConfigurationStore` (S7-S12-audit M2).
 */
export const ConfigurationPage = observer(function ConfigurationPage({
  admin,
}: {
  admin: IAdminClient;
}) {
  const store = useMemo(() => new ConfigurationStore({ admin }), [admin]);
  useEffect(() => {
    void store.start();
    return () => store.dispose();
  }, [store]);

  const data = store.data;
  const eff = data?.config.effective ?? null;

  return (
    <Stack spacing={3}>
      <Stack direction="row" alignItems="baseline" spacing={2} flexWrap="wrap" useFlexGap>
        <Typography variant="h4">Configuration</Typography>
        <Chip label="S10" size="small" variant="outlined" />
        {data?.config.overrideActive && (
          <Chip label="override active" size="small" color="warning" />
        )}
        {data?.config.restartRequired && (
          <Chip label="restart required" size="small" color="error" />
        )}
      </Stack>

      {store.status === "loading" && <LinearProgress />}
      {store.error && <Alert severity="error">{store.error}</Alert>}

      {eff && (
        <>
          <Card>
            <CardHeader title="Selected providers" />
            <CardContent>
              <Stack divider={<Divider flexItem />} spacing={1}>
                <Row k="Realtime primary" v={eff.realtimePrimaryProvider ?? "—"} />
                <Row k="RawTx primary" v={eff.rawTxPrimaryProvider ?? "—"} />
                <Row k="REST primary" v={eff.restPrimaryProvider ?? "—"} />
                <Row k="Bitails transport" v={eff.bitailsTransport ?? "—"} />
              </Stack>
            </CardContent>
          </Card>

          <Card>
            <CardHeader title="Bitails" />
            <CardContent>
              <Stack divider={<Divider flexItem />} spacing={1}>
                <Row k="Base URL" v={eff.bitails.baseUrl ?? "—"} />
                <Row k="Websocket URL" v={eff.bitails.websocketBaseUrl ?? "—"} />
                <Row k="ZMQ tx" v={eff.bitails.zmqTxUrl ?? "—"} />
                <Row k="ZMQ block" v={eff.bitails.zmqBlockUrl ?? "—"} />
                <Row k="API key" v={mask(eff.bitails.apiKey)} />
              </Stack>
            </CardContent>
          </Card>

          <Card>
            <CardHeader title="WhatsOnChain" />
            <CardContent>
              <Stack divider={<Divider flexItem />} spacing={1}>
                <Row k="Base URL" v={eff.whatsonchain.baseUrl ?? "—"} />
                <Row k="API key" v={mask(eff.whatsonchain.apiKey)} />
              </Stack>
            </CardContent>
          </Card>

          <Card>
            <CardHeader title="JungleBus" />
            <CardContent>
              <Stack divider={<Divider flexItem />} spacing={1}>
                <Row k="Base URL" v={eff.junglebus.baseUrl ?? "—"} />
                <Row k="Mempool subscription" v={eff.junglebus.mempoolSubscriptionId ?? "—"} />
                <Row k="Block subscription" v={eff.junglebus.blockSubscriptionId ?? "—"} />
              </Stack>
            </CardContent>
          </Card>
        </>
      )}
    </Stack>
  );
});

function Row({ k, v }: { k: string; v: string }) {
  return (
    <Stack direction="row" justifyContent="space-between" alignItems="baseline" spacing={2}>
      <Typography variant="body2" color="text.secondary">{k}</Typography>
      <Typography variant="code" sx={{ wordBreak: "break-all", textAlign: "right" }}>{v}</Typography>
    </Stack>
  );
}

function mask(v: string | null | undefined): string {
  if (!v) return "—";
  if (v.length <= 4) return "***";
  return `${v.slice(0, 2)}***${v.slice(-2)}`;
}
