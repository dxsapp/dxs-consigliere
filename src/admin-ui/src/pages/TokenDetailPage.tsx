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
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
} from "@mui/material";
import ArrowBackOutlinedIcon from "@mui/icons-material/ArrowBackOutlined";
import { tokenDetailStore } from "@/stores/token-detail.store";
import { tokenListStore } from "@/stores/token-list.store";
import { notifyStore } from "@/stores/notify.store";
import { ReadinessChip } from "@/components/ReadinessChip";
import { ConfirmDialog } from "@/components/ConfirmDialog";
import { TrustedRootsInput, parseTrustedRoots } from "@/components/TrustedRootsInput";
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
  "tokenId",
  "symbol",
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

// ─── Upgrade History Dialog ───────────────────────────────────────────────────

interface UpgradeDialogProps {
  open: boolean;
  tokenId: string;
  onClose: () => void;
}

function UpgradeHistoryDialog({ open, tokenId, onClose }: UpgradeDialogProps) {
  const [rootsRaw, setRootsRaw] = useState("");
  const [submitting, setSubmitting] = useState(false);

  const { roots, invalid } = parseTrustedRoots(rootsRaw);
  const canSubmit = roots.length > 0 && invalid.length === 0;

  const handleClose = () => {
    setRootsRaw("");
    onClose();
  };

  const handleSubmit = async () => {
    if (!canSubmit) return;
    setSubmitting(true);
    const result = await tokenDetailStore.upgradeHistory(tokenId, roots);
    setSubmitting(false);
    if (result.ok) {
      notifyStore.success("History upgrade queued.");
      handleClose();
    } else {
      notifyStore.error(result.error ?? "Failed to upgrade history.");
    }
  };

  return (
    <Dialog open={open} onClose={handleClose} maxWidth="sm" fullWidth>
      <DialogTitle>Queue Rooted Historical Backfill</DialogTitle>
      <DialogContent sx={{ pt: "16px !important" }}>
        <Typography variant="body2" sx={{ color: "text.secondary", mb: 2 }}>
          Provide trusted root transaction IDs for this token. The indexer will use them to expand
          rooted history inside the local managed scope. This is not unlimited token archaeology and
          may require higher-capacity provider access, additional disk space, and long-running
          backfill time.
        </Typography>
        <TrustedRootsInput value={rootsRaw} onChange={setRootsRaw} required />
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2 }}>
        <Button onClick={handleClose} color="inherit" disabled={submitting}>
          Cancel
        </Button>
        <Button
          onClick={handleSubmit}
          variant="contained"
          disabled={!canSubmit || submitting}
        >
          {submitting ? "Submitting…" : "Queue backfill"}
        </Button>
      </DialogActions>
    </Dialog>
  );
}

// ─── Page ─────────────────────────────────────────────────────────────────────

export const TokenDetailPage = observer(function TokenDetailPage() {
  const { tokenId: tokenIdParam } = useParams<{ tokenId: string }>();
  const navigate = useNavigate();

  const [untrackOpen, setUntrackOpen] = useState(false);
  const [untracking, setUntracking] = useState(false);
  const [upgradeOpen, setUpgradeOpen] = useState(false);
  const tokenId = tokenIdParam ? decodeURIComponent(tokenIdParam) : null;
  const store = tokenDetailStore;

  const current = store.current?.tokenId === tokenId ? store.current : null;
  const summary = store.summary;
  const loading =
    store.isLoading ||
    (!current && store.loadState !== "error" && store.loadState !== "not_found");

  if (loading) {
    return (
      <Box>
        <Button startIcon={<ArrowBackOutlinedIcon />} onClick={() => navigate("/tokens")} sx={{ mb: 2 }}>
          Tokens
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
        <Button startIcon={<ArrowBackOutlinedIcon />} onClick={() => navigate("/tokens")} sx={{ mb: 2 }}>
          Tokens
        </Button>
        <Alert severity="warning">Token not found.</Alert>
      </Box>
    );
  }

  if (store.loadState === "error" && !current) {
    return (
      <Box>
        <Button startIcon={<ArrowBackOutlinedIcon />} onClick={() => navigate("/tokens")} sx={{ mb: 2 }}>
          Tokens
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

  const handleUntrack = async () => {
    if (!tokenId) return;
    setUntracking(true);
    const result = await store.untrack(tokenId);
    setUntracking(false);
    setUntrackOpen(false);
    if (result.ok) {
      tokenListStore.invalidate();
      notifyStore.success("Token untracked.");
      navigate("/tokens");
    } else {
      notifyStore.error(result.error ?? "Failed to untrack.");
    }
  };

  return (
    <Box>
      <Button
        startIcon={<ArrowBackOutlinedIcon />}
        onClick={() => navigate("/tokens")}
        sx={{ mb: 2, color: "text.secondary" }}
      >
        Tokens
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
              {current.tokenId}
            </Typography>
            {current.symbol && (
              <Chip
                label={current.symbol}
                color="primary"
                size="small"
                variant="outlined"
                sx={{ height: 22, fontWeight: 600 }}
              />
            )}
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
          <InfoRow label="Protocol">
            <Typography variant="body2" sx={{ fontFamily: "monospace", fontSize: "0.82rem" }}>
              {current.protocolType ?? summary?.protocolType ?? "unavailable"}
              {current.protocolVersion ?? summary?.protocolVersion ? ` · ${current.protocolVersion ?? summary?.protocolVersion}` : ""}
            </Typography>
          </InfoRow>
          <InfoRow label="Validation">
            <Typography variant="body2" sx={{ fontFamily: "monospace", fontSize: "0.82rem" }}>
              {current.validationStatus ?? summary?.validationStatus ?? "unavailable"}
            </Typography>
          </InfoRow>
          <InfoRow label="Issuer">
            <Typography variant="body2" sx={{ fontFamily: "monospace", fontSize: "0.82rem", wordBreak: "break-all" }}>
              {current.issuer ?? summary?.issuer ?? "unavailable"}
            </Typography>
          </InfoRow>
          <InfoRow label="Redeem address">
            <Typography variant="body2" sx={{ fontFamily: "monospace", fontSize: "0.82rem", wordBreak: "break-all" }}>
              {current.redeemAddress ?? summary?.redeemAddress ?? "unavailable"}
            </Typography>
          </InfoRow>
          <InfoRow label="Total known supply">
            <Typography variant="body2" sx={{ fontFamily: "monospace", fontSize: "0.82rem" }}>
              {formatSatoshis(current.totalKnownSupply ?? summary?.totalKnownSupply ?? null)}
            </Typography>
          </InfoRow>
          <InfoRow label="Burned satoshis">
            <Typography variant="body2" sx={{ fontFamily: "monospace", fontSize: "0.82rem" }}>
              {formatSatoshis(current.burnedSatoshis ?? summary?.burnedSatoshis ?? null)}
            </Typography>
          </InfoRow>
          <InfoRow label="Last indexed height">
            <Typography variant="body2" sx={{ fontFamily: "monospace", fontSize: "0.82rem" }}>
              {formatCount(current.lastIndexedHeight ?? summary?.lastIndexedHeight ?? null)}
            </Typography>
          </InfoRow>
          <InfoRow label="Holder count">
            <Typography variant="body2" sx={{ fontFamily: "monospace", fontSize: "0.82rem" }}>
              {formatCount(summary?.holderCount ?? current.holderCount ?? null)}
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
              {((current.firstTransactionHeight ?? summary?.firstActivityHeight) != null)
                ? ` · height ${current.firstTransactionHeight ?? summary?.firstActivityHeight}`
                : ""}
            </Typography>
          </InfoRow>
          <InfoRow label="Last activity">
            <Typography variant="body2" sx={{ fontFamily: "monospace", fontSize: "0.82rem" }}>
              {formatDate(current.lastTransactionAt ?? summary?.lastActivityAt ?? null)}
              {((current.lastTransactionHeight ?? summary?.lastActivityHeight) != null)
                ? ` · height ${current.lastTransactionHeight ?? summary?.lastActivityHeight}`
                : ""}
            </Typography>
          </InfoRow>
          <InfoRow label="History readiness">
            <ReadinessChip readiness={current.readiness.history?.historyReadiness ?? summary?.historyReadiness ?? "unavailable"} />
          </InfoRow>
          <InfoRow label="History model">
            <Typography variant="body2" sx={{ color: "text.secondary" }}>
              Scoped rooted history. Current token state is authoritative inside the trusted-root
              managed scope; full chain archaeology is intentionally out of scope by default.
            </Typography>
          </InfoRow>
        </CardContent>
      </Card>

      {current.readiness.history?.rootedToken && (
        <Card variant="outlined" sx={{ mb: 2 }}>
          <CardContent sx={{ py: 2 }}>
            <Typography variant="overline" sx={{ color: "text.disabled", fontSize: "0.68rem" }}>
              Rooted History
            </Typography>
            <Divider sx={{ my: 1 }} />
            <InfoRow label="Trusted roots">
              <Typography variant="body2">
                {current.readiness.history.rootedToken.trustedRootCount}
              </Typography>
            </InfoRow>
            <InfoRow label="Completed roots">
              <Typography variant="body2">
                {current.readiness.history.rootedToken.completedTrustedRootCount}
              </Typography>
            </InfoRow>
            <InfoRow label="Unknown roots">
              <Typography variant="body2">
                {current.readiness.history.rootedToken.unknownRootFindingCount}
              </Typography>
            </InfoRow>
            <InfoRow label="Rooted secure">
              <Typography variant="body2">
                {current.readiness.history.rootedToken.rootedHistorySecure ? "Yes" : "No"}
              </Typography>
            </InfoRow>
            <InfoRow label="Blocking unknown root">
              <Typography variant="body2">
                {current.readiness.history.rootedToken.blockingUnknownRoot ? "Yes" : "No"}
              </Typography>
            </InfoRow>
          </CardContent>
        </Card>
      )}

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
              Rooted historical backfill can require paid or higher-capacity provider access,
              significant disk usage, and long-running sync time. For a clean operational boundary,
              move tokens into a fresh address and continue tracking from there.
            </Alert>
            <Box sx={{ display: "flex", gap: 1.5, flexWrap: "wrap", mt: 1.5 }}>
              <Button
                variant="outlined"
                size="small"
                onClick={() => setUpgradeOpen(true)}
              >
                Queue rooted backfill
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

      {tokenId && (
        <UpgradeHistoryDialog
          open={upgradeOpen}
          tokenId={tokenId}
          onClose={() => setUpgradeOpen(false)}
        />
      )}

      <ConfirmDialog
        open={untrackOpen}
        title="Untrack Token"
        message={`Remove tracking for ${current.tokenId}? Existing data will be tombstoned.`}
        confirmLabel="Untrack"
        dangerous
        loading={untracking}
        onConfirm={handleUntrack}
        onCancel={() => setUntrackOpen(false)}
      />
    </Box>
  );
});
