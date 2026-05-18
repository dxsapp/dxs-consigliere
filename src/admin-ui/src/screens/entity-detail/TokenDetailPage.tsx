import {
  Alert,
  Card,
  CardContent,
  Chip,
  Divider,
  LinearProgress,
  Stack,
  Typography,
} from "@mui/material";
import { useEffect, useState } from "react";
import { useParams } from "react-router-dom";
import { EntityTimeline } from "@/screens/entity-detail/EntityTimeline";
import { readinessStages } from "@/screens/entity-detail/tracking-stages";
import type { IAdminClient } from "@/lib/admin/admin-client";
import type { AdminTrackedTokenResponse } from "@/types/admin";

/**
 * S5 — Token detail screen. Same shape as Address detail: shared
 * vertical timeline drives readiness, summary card carries the
 * protocol-level counters.
 */
export function TokenDetailPage({ admin }: { admin: IAdminClient }) {
  const { tokenId = "" } = useParams<{ tokenId: string }>();
  const [state, setState] = useState<{
    status: "loading" | "ready" | "error";
    data: AdminTrackedTokenResponse | null;
    error: string | null;
  }>({ status: "loading", data: null, error: null });

  useEffect(() => {
    if (!tokenId) return;
    const ctl = new AbortController();
    setState({ status: "loading", data: null, error: null });
    admin
      .getTrackedToken(tokenId, ctl.signal)
      .then((data) => setState({ status: "ready", data, error: null }))
      .catch((err) => {
        if (ctl.signal.aborted) return;
        setState({
          status: "error",
          data: null,
          error: err instanceof Error ? err.message : "Failed to load token",
        });
      });
    return () => ctl.abort();
  }, [tokenId, admin]);

  const data = state.data;
  const stages = readinessStages(data?.readiness ?? null);

  return (
    <Stack spacing={3}>
      <Stack direction="row" alignItems="baseline" spacing={2} flexWrap="wrap" useFlexGap>
        <Typography variant="h4">Token</Typography>
        <Chip label="S5" size="small" variant="outlined" />
        {data?.symbol && <Chip label={data.symbol} size="small" />}
        {data?.isTombstoned && (
          <Chip label="Tombstoned" size="small" color="warning" />
        )}
        {data?.integritySafe === false && (
          <Chip label="Integrity check failed" size="small" color="error" />
        )}
      </Stack>

      <Card>
        <CardContent>
          <Stack spacing={1}>
            <Typography variant="overline" color="text.secondary">
              tokenId
            </Typography>
            <Typography variant="code" sx={{ wordBreak: "break-all" }}>
              {tokenId || "(missing)"}
            </Typography>
          </Stack>
        </CardContent>
      </Card>

      {state.status === "loading" && <LinearProgress />}
      {state.status === "error" && state.error && (
        <Alert severity="error">{state.error}</Alert>
      )}

      <Card>
        <CardContent>
          <Stack spacing={2}>
            <Typography variant="overline" color="text.secondary">
              Tracking readiness
            </Typography>
            <EntityTimeline stages={stages} ariaLabel="token tracking readiness" />
          </Stack>
        </CardContent>
      </Card>

      {data?.summary && (
        <Card>
          <CardContent>
            <Stack spacing={1.5}>
              <Typography variant="overline" color="text.secondary">
                Summary
              </Typography>
              <Stack divider={<Divider flexItem />} spacing={1}>
                <Row k="Protocol" v={data.summary.protocolType} />
                <Row k="Validation" v={data.summary.validationStatus} />
                {data.summary.issuer && <Row k="Issuer" v={data.summary.issuer} />}
                {data.summary.redeemAddress && (
                  <Row k="Redeem address" v={data.summary.redeemAddress} />
                )}
                {data.summary.localKnownSupplySatoshis !== null && (
                  <Row
                    k="Known supply"
                    v={data.summary.localKnownSupplySatoshis.toLocaleString()}
                  />
                )}
                {data.summary.burnedSatoshis !== null && (
                  <Row k="Burned" v={data.summary.burnedSatoshis.toLocaleString()} />
                )}
                <Row k="Holders" v={`${data.summary.holderCount}`} />
                <Row k="UTXOs" v={`${data.summary.utxoCount}`} />
                <Row k="Transactions" v={`${data.summary.transactionCount}`} />
              </Stack>
            </Stack>
          </CardContent>
        </Card>
      )}
    </Stack>
  );
}

function Row({ k, v }: { k: string; v: string }) {
  return (
    <Stack direction="row" justifyContent="space-between" alignItems="baseline" spacing={2}>
      <Typography variant="body2" color="text.secondary">
        {k}
      </Typography>
      <Typography variant="body2">{v}</Typography>
    </Stack>
  );
}
