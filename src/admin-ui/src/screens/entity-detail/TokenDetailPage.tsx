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
import { observer } from "mobx-react-lite";
import { useEffect, useMemo } from "react";
import { useParams } from "react-router-dom";
import { EntityTimeline } from "@/screens/entity-detail/EntityTimeline";
import { readinessStages } from "@/screens/entity-detail/tracking-stages";
import { TokenDetailStore } from "@/screens/entity-detail/token-detail.store";
import type { IAdminClient } from "@/lib/admin/admin-client";

/**
 * S5 — Token detail screen. Same shape as Address detail: shared
 * vertical timeline drives readiness, summary card carries the
 * protocol-level counters. Lifecycle owned by `TokenDetailStore`
 * (S5-audit M1) — page is a render shell.
 */
export const TokenDetailPage = observer(function TokenDetailPage({
  admin,
}: {
  admin: IAdminClient;
}) {
  const { tokenId = "" } = useParams<{ tokenId: string }>();
  const store = useMemo(
    () => new TokenDetailStore({ admin, tokenId }),
    [admin, tokenId]
  );
  useEffect(() => {
    void store.start();
    return () => store.dispose();
  }, [store]);

  const data = store.data;
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

      {store.status === "loading" && <LinearProgress />}
      {store.status === "error" && store.error && (
        <Alert severity="error">{store.error}</Alert>
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
                <Row k="Protocol" v={data.summary.protocolType ?? "—"} />
                <Row k="Validation" v={data.summary.validationStatus ?? "—"} />
                {data.summary.issuer && <Row k="Issuer" v={data.summary.issuer} />}
                {data.summary.redeemAddress && (
                  <Row k="Redeem address" v={data.summary.redeemAddress} />
                )}
                {data.summary.localKnownSupplySatoshis != null && (
                  <Row
                    k="Known supply"
                    v={data.summary.localKnownSupplySatoshis.toLocaleString()}
                  />
                )}
                {data.summary.burnedSatoshis != null && (
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
});

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
