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
import type { AdminProvidersResponse } from "@/types/admin";

/**
 * S10 — Configuration screen. Read-only sectioned view of the
 * effective provider config (Bitails / WhatsOnChain / JungleBus).
 * No `tune` affordance per the design brief — operators change
 * config out-of-band via env / config files and reload.
 */
export function ConfigurationPage({ admin }: { admin: IAdminClient }) {
  const [data, setData] = useState<AdminProvidersResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [status, setStatus] = useState<"loading" | "ready" | "error">("loading");

  useEffect(() => {
    const ctl = new AbortController();
    admin
      .getProviders(ctl.signal)
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

      {status === "loading" && <LinearProgress />}
      {error && <Alert severity="error">{error}</Alert>}

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
}

function Row({ k, v }: { k: string; v: string }) {
  return (
    <Stack direction="row" justifyContent="space-between" alignItems="baseline" spacing={2}>
      <Typography variant="body2" color="text.secondary">{k}</Typography>
      <Typography variant="code" sx={{ wordBreak: "break-all", textAlign: "right" }}>{v}</Typography>
    </Stack>
  );
}

function mask(v: string | null): string {
  if (!v) return "—";
  if (v.length <= 4) return "***";
  return `${v.slice(0, 2)}***${v.slice(-2)}`;
}
