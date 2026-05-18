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
import type { AdminTrackedAddressResponse } from "@/types/admin";

/**
 * S5 — Address detail screen. Reuses the shared EntityTimeline for
 * the tracking-readiness lifecycle plus a summary card with the
 * key balance + UTXO counters.
 */
export function AddressDetailPage({ admin }: { admin: IAdminClient }) {
  const { address = "" } = useParams<{ address: string }>();
  const [state, setState] = useState<{
    status: "loading" | "ready" | "error";
    data: AdminTrackedAddressResponse | null;
    error: string | null;
  }>({ status: "loading", data: null, error: null });

  useEffect(() => {
    if (!address) return;
    const ctl = new AbortController();
    setState({ status: "loading", data: null, error: null });
    admin
      .getTrackedAddress(address, ctl.signal)
      .then((data) => setState({ status: "ready", data, error: null }))
      .catch((err) => {
        if (ctl.signal.aborted) return;
        setState({
          status: "error",
          data: null,
          error: err instanceof Error ? err.message : "Failed to load address",
        });
      });
    return () => ctl.abort();
  }, [address, admin]);

  const data = state.data;
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
                        label={`${tb.symbol} · ${formatSats(tb.balanceSatoshis)}`}
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

function formatSats(sats: number): string {
  return (sats / 100_000_000).toLocaleString(undefined, {
    minimumFractionDigits: 8,
    maximumFractionDigits: 8,
  });
}
