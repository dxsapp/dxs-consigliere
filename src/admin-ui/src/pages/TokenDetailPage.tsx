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
  Collapse,
  IconButton,
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
} from "@mui/material";
import ArrowBackOutlinedIcon from "@mui/icons-material/ArrowBackOutlined";
import ExpandMoreOutlinedIcon from "@mui/icons-material/ExpandMoreOutlined";
import ExpandLessOutlinedIcon from "@mui/icons-material/ExpandLessOutlined";
import { tokenDetailStore } from "@/stores/token-detail.store";
import { tokenListStore } from "@/stores/token-list.store";
import { notifyStore } from "@/stores/notify.store";
import { ReadinessChip } from "@/components/ReadinessChip";
import { ConfirmDialog } from "@/components/ConfirmDialog";
import { TrustedRootsInput, parseTrustedRoots } from "@/components/TrustedRootsInput";

// ─── Helpers ─────────────────────────────────────────────────────────────────

function formatDate(value: number | null | undefined): string {
  if (value == null) return "—";
  return new Intl.DateTimeFormat("en-GB", { dateStyle: "medium", timeStyle: "short" }).format(
    new Date(value),
  );
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
      <DialogTitle>Upgrade to Full History</DialogTitle>
      <DialogContent sx={{ pt: "16px !important" }}>
        <Typography variant="body2" sx={{ color: "text.secondary", mb: 2 }}>
          Provide the genesis (root) transaction IDs for this token. The indexer will use these to
          backfill the full transaction history.
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
          {submitting ? "Submitting…" : "Upgrade"}
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
  const [rawExpanded, setRawExpanded] = useState(false);

  const tokenId = tokenIdParam ? decodeURIComponent(tokenIdParam) : null;
  const store = tokenDetailStore;

  const current = store.current?.tokenId === tokenId ? store.current : null;
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
            <Box sx={{ display: "flex", gap: 1.5, flexWrap: "wrap", mt: 1.5 }}>
              <Button
                variant="outlined"
                size="small"
                onClick={() => setUpgradeOpen(true)}
              >
                Upgrade to Full History
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

      {/* Raw payload */}
      {hasExtra && (
        <Card variant="outlined">
          <CardContent sx={{ py: 2 }}>
            <Box
              sx={{
                display: "flex",
                alignItems: "center",
                justifyContent: "space-between",
                cursor: "pointer",
              }}
              onClick={() => setRawExpanded((v) => !v)}
            >
              <Typography variant="overline" sx={{ color: "text.disabled", fontSize: "0.68rem" }}>
                Raw Payload
              </Typography>
              <IconButton size="small" sx={{ color: "text.disabled" }}>
                {rawExpanded ? (
                  <ExpandLessOutlinedIcon fontSize="small" />
                ) : (
                  <ExpandMoreOutlinedIcon fontSize="small" />
                )}
              </IconButton>
            </Box>
            <Collapse in={rawExpanded}>
              <Divider sx={{ my: 1 }} />
              <Box
                component="pre"
                sx={{
                  fontSize: "0.75rem",
                  fontFamily: "monospace",
                  color: "text.secondary",
                  bgcolor: "background.default",
                  borderRadius: 1,
                  p: 1.5,
                  overflow: "auto",
                  maxHeight: 400,
                  m: 0,
                }}
              >
                {JSON.stringify(extraFields, null, 2)}
              </Box>
            </Collapse>
          </CardContent>
        </Card>
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
