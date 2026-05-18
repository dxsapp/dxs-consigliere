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
import { AddressDetailStore } from "@/screens/entity-detail/address-detail.store";
import { EntityTimeline } from "@/screens/entity-detail/EntityTimeline";
import { readinessStages } from "@/screens/entity-detail/tracking-stages";
import type { IAdminClient } from "@/lib/admin/admin-client";

/**
 * S5 — Address detail screen. Reuses the shared EntityTimeline for
 * the tracking-readiness lifecycle plus a summary card with the
 * key balance + UTXO counters. Lifecycle owned by
 * `AddressDetailStore` (S5-audit M1) — page is a render shell.
 */
export const AddressDetailPage = observer(function AddressDetailPage({
  admin,
}: {
  admin: IAdminClient;
}) {
  const { address = "" } = useParams<{ address: string }>();
  const store = useMemo(
    () => new AddressDetailStore({ admin, address }),
    [admin, address]
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
        <Typography variant="h4">Address</Typography>
        <Chip label="S5" size="small" variant="outlined" />
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
              address
            </Typography>
            <Typography variant="code" sx={{ wordBreak: "break-all" }}>
              {address || "(missing)"}
            </Typography>
            {data?.name && (
              <Typography variant="body2" color="text.secondary">
                {data.name}
              </Typography>
            )}
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
            <EntityTimeline stages={stages} ariaLabel="address tracking readiness" />
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
                <Row k="Balance" v={`${formatSats(data.summary.currentBsvBalanceSatoshis)} BSV`} />
                <Row k="Total UTXOs" v={`${data.summary.totalUtxoCount}`} />
                <Row k="BSV UTXOs" v={`${data.summary.bsvUtxoCount}`} />
                <Row k="Token UTXOs" v={`${data.summary.tokenUtxoCount}`} />
                <Row k="Transactions" v={`${data.summary.transactionCount}`} />
                {data.summary.firstTransactionAt && (
                  <Row
                    k="First tx"
                    v={`${new Date(data.summary.firstTransactionAt).toUTCString().slice(5, 16)} · h ${data.summary.firstTransactionBlockHeight ?? "—"}`}
                  />
                )}
                {data.summary.lastTransactionAt && (
                  <Row
                    k="Last tx"
                    v={`${new Date(data.summary.lastTransactionAt).toUTCString().slice(5, 16)} · h ${data.summary.lastTransactionBlockHeight ?? "—"}`}
                  />
                )}
              </Stack>
              {data.summary.tokenBalances.length > 0 && (
                <>
                  <Typography variant="overline" color="text.secondary" sx={{ mt: 1 }}>
                    Token balances
                  </Typography>
                  <Stack direction="row" spacing={1} useFlexGap flexWrap="wrap">
                    {data.summary.tokenBalances.map((tb) => (
                      <Chip
                        key={tb.tokenId}
                        label={`${shortenTokenId(tb.tokenId)} · ${tb.satoshis.toLocaleString()} sats`}
                        size="small"
                        variant="outlined"
                      />
                    ))}
                  </Stack>
                </>
              )}
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

function formatSats(sats: number): string {
  return (sats / 100_000_000).toLocaleString(undefined, {
    minimumFractionDigits: 8,
    maximumFractionDigits: 8,
  });
}

function shortenTokenId(id: string): string {
  return id.length > 16 ? `${id.slice(0, 8)}…${id.slice(-6)}` : id;
}
