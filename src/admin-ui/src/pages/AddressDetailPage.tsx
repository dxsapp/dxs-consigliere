import { useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { observer } from "mobx-react-lite";
import {
  Box,
  Typography,
  Button,
  Chip,
  Card,
  CardContent,
  Divider,
  Alert,
  Skeleton,
  Tooltip,
} from "@mui/material";
import ArrowBackOutlinedIcon from "@mui/icons-material/ArrowBackOutlined";
import { addressDetailStore } from "@/stores/address-detail.store";
import { addressListStore } from "@/stores/address-list.store";
import { notifyStore } from "@/stores/notify.store";
import { ReadinessChip } from "@/components/ReadinessChip";
import { ConfirmDialog } from "@/components/ConfirmDialog";
import { JsonPanel } from "@/components/JsonPanel";

// ─── Helpers ─────────────────────────────────────────────────────────────────

function formatDate(value: number | null | undefined): string {
  if (value == null) return "—";
  return new Intl.DateTimeFormat("en-GB", { dateStyle: "medium", timeStyle: "short" }).format(
    new Date(value),
  );
}

function formatCount(value: number | null | undefined): string {
  if (value == null) return "unavailable";
  return new Intl.NumberFormat("en-GB").format(value);
}

function formatSatoshis(value: number | null | undefined): string {
  if (value == null) return "unavailable";
  return `${new Intl.NumberFormat("en-GB").format(value)} sat`;
}

const KNOWN_KEYS = new Set([
  "address",
  "name",
  "isTombstoned",
  "tombstonedAt",
  "createdAt",
  "updatedAt",
  "failureReason",
  "integritySafe",
  "readiness",
]);

function InfoRow({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <Box sx={{ display: "flex", alignItems: "flex-start", py: 0.75, gap: 2 }}>
      <Typography
        variant="body2"
        sx={{ color: "text.disabled", fontSize: "0.78rem", minWidth: 140, flexShrink: 0 }}
      >
        {label}
      </Typography>
      <Box sx={{ flex: 1 }}>{children}</Box>
    </Box>
  );
}

// ─── Page ─────────────────────────────────────────────────────────────────────

export const AddressDetailPage = observer(function AddressDetailPage() {
  const { address: addressParam } = useParams<{ address: string }>();
  const navigate = useNavigate();

  const [untrackOpen, setUntrackOpen] = useState(false);
  const [untracking, setUntracking] = useState(false);
  const [upgrading, setUpgrading] = useState(false);
  const address = addressParam ? decodeURIComponent(addressParam) : null;
  const store = addressDetailStore;

  const current = store.current?.address === address ? store.current : null;
  const summary = store.summary;
  const loading =
    store.isLoading ||
    (!current && store.loadState !== "error" && store.loadState !== "not_found");

  if (loading) {
    return (
      <Box>
        <Button startIcon={<ArrowBackOutlinedIcon />} onClick={() => navigate("/addresses")} sx={{ mb: 2 }}>
          Addresses
        </Button>
        <Skeleton variant="text" width={320} height={36} sx={{ mb: 1 }} />
        <Skeleton variant="rounded" height={180} sx={{ mb: 2 }} />
        <Skeleton variant="rounded" height={100} />
      </Box>
    );
  }

  if (store.loadState === "not_found") {
    return (
      <Box>
        <Button startIcon={<ArrowBackOutlinedIcon />} onClick={() => navigate("/addresses")} sx={{ mb: 2 }}>
          Addresses
        </Button>
        <Alert severity="warning">Address not found.</Alert>
      </Box>
    );
  }

  if (store.loadState === "error" && !current) {
    return (
      <Box>
        <Button startIcon={<ArrowBackOutlinedIcon />} onClick={() => navigate("/addresses")} sx={{ mb: 2 }}>
          Addresses
        </Button>
        <Alert
          severity="error"
          action={
            <Button color="inherit" size="small" onClick={() => store.reload()}>
              Retry
            </Button>
          }
        >
          {store.error}
        </Alert>
      </Box>
    );
  }

  if (!current) return null;

  const extraFields = Object.fromEntries(
    Object.entries(current).filter(([k]) => !KNOWN_KEYS.has(k)),
  );
  const hasExtra = Object.keys(extraFields).length > 0;

  const handleUpgradeHistory = async () => {
    if (!address) return;
    setUpgrading(true);
    const result = await store.upgradeHistory(address);
    setUpgrading(false);
    if (result.ok) {
      notifyStore.success("History upgrade queued.");
    } else {
      notifyStore.error(result.error ?? "Failed to upgrade history.");
    }
  };

  const handleUntrack = async () => {
    if (!address) return;
    setUntracking(true);
    const result = await store.untrack(address);
    setUntracking(false);
    setUntrackOpen(false);
    if (result.ok) {
      addressListStore.invalidate();
      notifyStore.success("Address untracked.");
      navigate("/addresses");
    } else {
      notifyStore.error(result.error ?? "Failed to untrack.");
    }
  };

  return (
    <Box>
      <Button
        startIcon={<ArrowBackOutlinedIcon />}
        onClick={() => navigate("/addresses")}
        sx={{ mb: 2, color: "text.secondary" }}
      >
        Addresses
      </Button>

      {/* Header */}
      <Box sx={{ display: "flex", alignItems: "flex-start", gap: 2, mb: 3, flexWrap: "wrap" }}>
        <Box sx={{ flex: 1, minWidth: 0 }}>
          <Box sx={{ display: "flex", alignItems: "center", gap: 1.5, flexWrap: "wrap" }}>
            <Typography
              variant="h5"
              sx={{
                fontFamily: "monospace",
                fontWeight: 600,
                letterSpacing: "-0.01em",
                wordBreak: "break-all",
              }}
            >
              {current.address}
            </Typography>
            <ReadinessChip readiness={current.readiness.lifecycleStatus} />
            <ReadinessChip
              readiness={current.readiness.history?.historyReadiness ?? "not_requested"}
            />
            {current.isTombstoned && (
              <Chip
                label="tombstoned"
                color="default"
                size="small"
                variant="outlined"
                sx={{ height: 22 }}
              />
            )}
          </Box>
          {current.name && (
            <Typography variant="body2" sx={{ color: "text.secondary", mt: 0.5 }}>
              {current.name}
            </Typography>
          )}
        </Box>
      </Box>

      {current.failureReason && (
        <Alert severity="error" sx={{ mb: 2, fontFamily: "monospace", fontSize: "0.8rem" }}>
          {current.failureReason}
        </Alert>
      )}

      {/* Status */}
      <Card variant="outlined" sx={{ mb: 2 }}>
        <CardContent sx={{ py: 2 }}>
          <Typography variant="overline" sx={{ color: "text.disabled", fontSize: "0.68rem" }}>
            Status
          </Typography>
          <Divider sx={{ my: 1 }} />
          <InfoRow label="Readiness">
            <Box sx={{ display: "flex", gap: 1, flexWrap: "wrap" }}>
              <ReadinessChip readiness={current.readiness.lifecycleStatus} />
              <ReadinessChip
                readiness={current.readiness.history?.historyReadiness ?? "not_requested"}
              />
            </Box>
          </InfoRow>
          <InfoRow label="Readable">
            <Typography variant="body2">{current.readiness.readable ? "Yes" : "No"}</Typography>
          </InfoRow>
          <InfoRow label="Authoritative">
            <Typography variant="body2">{current.readiness.authoritative ? "Yes" : "No"}</Typography>
          </InfoRow>
          <InfoRow label="Degraded">
            <Typography variant="body2">{current.readiness.degraded ? "Yes" : "No"}</Typography>
          </InfoRow>
          <InfoRow label="Integrity">
            <Chip
              label={current.integritySafe ? "safe" : "unsafe"}
              color={current.integritySafe ? "success" : "error"}
              size="small"
              variant="outlined"
              sx={{ height: 22, fontSize: "0.7rem" }}
            />
          </InfoRow>
          <InfoRow label="Tombstoned">
            <Typography variant="body2">{current.isTombstoned ? "Yes" : "No"}</Typography>
          </InfoRow>
          {current.tombstonedAt && (
            <InfoRow label="Tombstoned at">
              <Typography variant="body2" sx={{ fontFamily: "monospace", fontSize: "0.8rem" }}>
                {formatDate(current.tombstonedAt)}
              </Typography>
            </InfoRow>
          )}
        </CardContent>
      </Card>

      {/* Operational summary */}
      <Card variant="outlined" sx={{ mb: 2 }}>
        <CardContent sx={{ py: 2 }}>
          <Typography variant="overline" sx={{ color: "text.disabled", fontSize: "0.68rem" }}>
            Operational summary
          </Typography>
          <Divider sx={{ my: 1 }} />
          <InfoRow label="BSV balance">
            <Typography variant="body2" sx={{ fontFamily: "monospace", fontSize: "0.82rem" }}>
              {formatSatoshis(current.balanceSatoshis ?? summary?.bsvBalanceSatoshis ?? null)}
            </Typography>
          </InfoRow>
          <InfoRow label="UTXO count">
            <Typography variant="body2" sx={{ fontFamily: "monospace", fontSize: "0.82rem" }}>
              {formatCount(current.utxoCount ?? summary?.utxoCount ?? null)}
            </Typography>
          </InfoRow>
          <InfoRow label="Transaction count">
            <Typography variant="body2" sx={{ fontFamily: "monospace", fontSize: "0.82rem" }}>
              {formatCount(current.transactionCount ?? summary?.transactionCount ?? null)}
            </Typography>
          </InfoRow>
          <InfoRow label="First activity">
            <Typography variant="body2" sx={{ fontFamily: "monospace", fontSize: "0.82rem" }}>
              {formatDate(current.firstTransactionAt ?? summary?.firstActivityAt ?? null)}
              {current.firstTransactionHeight ?? summary?.firstActivityHeight ?? null != null
                ? ` · height ${current.firstTransactionHeight ?? summary?.firstActivityHeight}`
                : ""}
            </Typography>
          </InfoRow>
          <InfoRow label="Last activity">
            <Typography variant="body2" sx={{ fontFamily: "monospace", fontSize: "0.82rem" }}>
              {formatDate(current.lastTransactionAt ?? summary?.lastActivityAt ?? null)}
              {current.lastTransactionHeight ?? summary?.lastActivityHeight ?? null != null
                ? ` · height ${current.lastTransactionHeight ?? summary?.lastActivityHeight}`
                : ""}
            </Typography>
          </InfoRow>
          <InfoRow label="History readiness">
            <Box sx={{ display: "flex", gap: 1, flexWrap: "wrap" }}>
              <ReadinessChip readiness={current.readiness.history?.historyReadiness ?? summary?.historyReadiness ?? "unavailable"} />
            </Box>
          </InfoRow>
          <InfoRow label="History model">
            <Typography variant="body2" sx={{ color: "text.secondary" }}>
              Scoped local history. Current state is authoritative inside the managed scope; older
              chain activity outside that scope may remain unresolved.
            </Typography>
          </InfoRow>
          <InfoRow label="Token balance snapshot">
            <Typography variant="body2" sx={{ color: "text.secondary" }}>
              {current.tokenBalanceSatoshis != null || current.tokenBalanceCount != null
                ? `${formatCount(current.tokenBalanceCount)} entries · ${formatSatoshis(current.tokenBalanceSatoshis)}`
                : "unavailable"}
            </Typography>
          </InfoRow>
        </CardContent>
      </Card>

      {/* Timestamps */}
      <Card variant="outlined" sx={{ mb: 2 }}>
        <CardContent sx={{ py: 2 }}>
          <Typography variant="overline" sx={{ color: "text.disabled", fontSize: "0.68rem" }}>
            Timestamps
          </Typography>
          <Divider sx={{ my: 1 }} />
          <InfoRow label="Tracked since">
            <Typography variant="body2" sx={{ fontFamily: "monospace", fontSize: "0.8rem" }}>
              {formatDate(current.createdAt)}
            </Typography>
          </InfoRow>
          <InfoRow label="Last updated">
            <Typography variant="body2" sx={{ fontFamily: "monospace", fontSize: "0.8rem" }}>
              {formatDate(current.updatedAt)}
            </Typography>
          </InfoRow>
        </CardContent>
      </Card>

      {/* Actions */}
      {!current.isTombstoned && (
        <Card variant="outlined" sx={{ mb: 2 }}>
          <CardContent sx={{ py: 2 }}>
            <Typography variant="overline" sx={{ color: "text.disabled", fontSize: "0.68rem" }}>
              Actions
            </Typography>
            <Divider sx={{ my: 1 }} />
            <Alert severity="info" sx={{ mb: 1.5 }}>
              Address history is intentionally scoped. Deeper backfill can require paid or
              higher-capacity provider access, significant disk usage, and long-running sync time.
              If you need a fresh operational boundary, move funds to a new address and track from
              there.
            </Alert>
            <Box sx={{ display: "flex", gap: 1.5, flexWrap: "wrap", mt: 1.5 }}>
              <Button
                variant="outlined"
                size="small"
                onClick={handleUpgradeHistory}
                disabled={upgrading}
              >
                {upgrading ? "Queueing…" : "Queue historical backfill"}
              </Button>
              <Tooltip
                title={
                  store.managedByConfig
                    ? "Managed by config — cannot untrack manually"
                    : ""
                }
              >
                <span>
                  <Button
                    variant="outlined"
                    color="error"
                    size="small"
                    disabled={store.managedByConfig}
                    onClick={() => setUntrackOpen(true)}
                  >
                    Untrack
                  </Button>
                </span>
              </Tooltip>
            </Box>
          </CardContent>
        </Card>
      )}

      {/* Additional fields */}
      {hasExtra && (
        <JsonPanel title="Additional fields" data={extraFields} />
      )}

      <ConfirmDialog
        open={untrackOpen}
        title="Untrack Address"
        message={`Remove tracking for ${current.address}? Existing data will be tombstoned.`}
        confirmLabel="Untrack"
        dangerous
        loading={untracking}
        onConfirm={handleUntrack}
        onCancel={() => setUntrackOpen(false)}
      />
    </Box>
  );
});
