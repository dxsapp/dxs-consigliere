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
} from "@mui/material";
import ArrowBackOutlinedIcon from "@mui/icons-material/ArrowBackOutlined";
import ExpandMoreOutlinedIcon from "@mui/icons-material/ExpandMoreOutlined";
import ExpandLessOutlinedIcon from "@mui/icons-material/ExpandLessOutlined";
import { addressDetailStore } from "@/stores/address-detail.store";
import { addressListStore } from "@/stores/address-list.store";
import { notifyStore } from "@/stores/notify.store";
import { ReadinessChip } from "@/components/ReadinessChip";
import { ConfirmDialog } from "@/components/ConfirmDialog";

// ─── Helpers ─────────────────────────────────────────────────────────────────

function formatDate(value: number | null | undefined): string {
  if (value == null) return "—";
  return new Intl.DateTimeFormat("en-GB", { dateStyle: "medium", timeStyle: "short" }).format(
    new Date(value),
  );
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
  const [rawExpanded, setRawExpanded] = useState(false);

  const address = addressParam ? decodeURIComponent(addressParam) : null;
  const store = addressDetailStore;

  const current = store.current?.address === address ? store.current : null;
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
                onClick={handleUpgradeHistory}
                disabled={upgrading}
              >
                {upgrading ? "Upgrading…" : "Upgrade to Full History"}
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
