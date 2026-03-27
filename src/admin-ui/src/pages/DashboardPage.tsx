import { useNavigate } from "react-router-dom";
import { observer } from "mobx-react-lite";
import {
  Box,
  Typography,
  Button,
  Card,
  CardContent,
  CardActionArea,
  Skeleton,
  Alert,
  IconButton,
  CircularProgress,
  Tooltip,
} from "@mui/material";
import RefreshOutlinedIcon from "@mui/icons-material/RefreshOutlined";
import { dashboardStore } from "@/stores/dashboard.store";
import { JsonPanel } from "@/components/JsonPanel";

// ─── Stat card ────────────────────────────────────────────────────────────────

type CardAccent = "default" | "success" | "warning" | "error" | "info";

const ACCENT_COLORS: Record<CardAccent, string> = {
  default: "transparent",
  success: "success.main",
  warning: "warning.main",
  error: "error.main",
  info: "info.main",
};

interface StatCardProps {
  label: string;
  value: number;
  accent?: CardAccent;
  sublabel?: string;
  onClick?: () => void;
}

function StatCard({ label, value, accent = "default", sublabel, onClick }: StatCardProps) {
  const accentColor = ACCENT_COLORS[accent];
  const inner = (
    <CardContent sx={{ p: 2, "&:last-child": { pb: 2 } }}>
      <Box sx={{ display: "flex", alignItems: "flex-start", justifyContent: "space-between" }}>
        <Typography
          variant="caption"
          sx={{ color: "text.disabled", fontSize: "0.7rem", textTransform: "uppercase", letterSpacing: "0.06em" }}
        >
          {label}
        </Typography>
        {accent !== "default" && (
          <Box
            sx={{
              width: 6,
              height: 6,
              borderRadius: "50%",
              bgcolor: accentColor,
              mt: 0.5,
              flexShrink: 0,
            }}
          />
        )}
      </Box>
      <Typography
        variant="h3"
        sx={{
          fontWeight: 700,
          letterSpacing: "-0.03em",
          mt: 1,
          color: accent !== "default" && value > 0 ? accentColor : "text.primary",
          lineHeight: 1,
        }}
      >
        {value}
      </Typography>
      {sublabel && (
        <Typography variant="caption" sx={{ color: "text.disabled", mt: 0.5, display: "block" }}>
          {sublabel}
        </Typography>
      )}
    </CardContent>
  );

  return (
    <Card
      variant="outlined"
      sx={{
        borderColor: accent !== "default" && value > 0 ? accentColor : "divider",
        transition: "border-color 0.2s",
      }}
    >
      {onClick ? (
        <CardActionArea onClick={onClick} sx={{ height: "100%" }}>
          {inner}
        </CardActionArea>
      ) : (
        inner
      )}
    </Card>
  );
}

// ─── Skeleton row ─────────────────────────────────────────────────────────────

function StatCardSkeleton() {
  return (
    <Card variant="outlined">
      <CardContent sx={{ p: 2, "&:last-child": { pb: 2 } }}>
        <Skeleton variant="text" width={80} height={14} />
        <Skeleton variant="text" width={48} height={48} sx={{ mt: 0.5 }} />
      </CardContent>
    </Card>
  );
}

// ─── Page ─────────────────────────────────────────────────────────────────────

export const DashboardPage = observer(function DashboardPage() {
  const navigate = useNavigate();
  const store = dashboardStore;

  if (store.isLoading) {
    return (
      <Box>
        <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", mb: 3 }}>
          <Typography variant="h5" sx={{ fontWeight: 600, letterSpacing: "-0.02em" }}>
            Dashboard
          </Typography>
        </Box>
        <Box sx={{ display: "grid", gridTemplateColumns: "repeat(4, 1fr)", gap: 2, mb: 2 }}>
          {Array.from({ length: 8 }).map((_, i) => (
            <StatCardSkeleton key={i} />
          ))}
        </Box>
      </Box>
    );
  }

  if (store.loadState === "error") {
    return (
      <Box>
        <Typography variant="h5" sx={{ fontWeight: 600, letterSpacing: "-0.02em", mb: 2 }}>
          Dashboard
        </Typography>
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

  const s = store.summary;

  return (
    <Box>
      {/* Header */}
      <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", mb: 3 }}>
        <Typography variant="h5" sx={{ fontWeight: 600, letterSpacing: "-0.02em" }}>
          Dashboard
        </Typography>
        <Tooltip title="Refresh">
          <IconButton
            size="small"
            onClick={() => store.reload()}
            disabled={store.refreshing}
            sx={{ color: "text.disabled" }}
          >
            {store.refreshing ? (
              <CircularProgress size={16} color="inherit" />
            ) : (
              <RefreshOutlinedIcon fontSize="small" />
            )}
          </IconButton>
        </Tooltip>
      </Box>

      {/* Primary — Entities */}
      <Typography variant="overline" sx={{ color: "text.disabled", fontSize: "0.68rem" }}>
        Entities
      </Typography>
      <Box sx={{ display: "grid", gridTemplateColumns: "repeat(2, 1fr)", gap: 2, mt: 1, mb: 3 }}>
        <StatCard
          label="Active Addresses"
          value={s?.activeAddressCount ?? 0}
          sublabel={
            s && s.tombstonedAddressCount > 0
              ? `${s.tombstonedAddressCount} tombstoned`
              : undefined
          }
          onClick={() => navigate("/addresses")}
        />
        <StatCard
          label="Active Tokens"
          value={s?.activeTokenCount ?? 0}
          sublabel={
            s && s.tombstonedTokenCount > 0
              ? `${s.tombstonedTokenCount} tombstoned`
              : undefined
          }
          onClick={() => navigate("/tokens")}
        />
      </Box>

      {/* Health */}
      <Typography variant="overline" sx={{ color: "text.disabled", fontSize: "0.68rem" }}>
        Health
      </Typography>
      <Box sx={{ display: "grid", gridTemplateColumns: "repeat(4, 1fr)", gap: 2, mt: 1, mb: 3 }}>
        <StatCard
          label="Degraded Addresses"
          value={s?.degradedAddressCount ?? 0}
          accent={s && s.degradedAddressCount > 0 ? "error" : "default"}
        />
        <StatCard
          label="Degraded Tokens"
          value={s?.degradedTokenCount ?? 0}
          accent={s && s.degradedTokenCount > 0 ? "error" : "default"}
        />
        <StatCard
          label="Failures"
          value={s?.failureCount ?? 0}
          accent={s && s.failureCount > 0 ? "error" : "default"}
          onClick={s && s.failureCount > 0 ? () => navigate("/findings") : undefined}
        />
        <StatCard
          label="Unknown Root Findings"
          value={s?.unknownRootFindingCount ?? 0}
          accent={s && s.unknownRootFindingCount > 0 ? "warning" : "default"}
          sublabel={
            s && s.blockingUnknownRootTokenCount > 0
              ? `${s.blockingUnknownRootTokenCount} blocking`
              : undefined
          }
          onClick={s && s.unknownRootFindingCount > 0 ? () => navigate("/findings") : undefined}
        />
      </Box>

      {/* Backfill */}
      <Typography variant="overline" sx={{ color: "text.disabled", fontSize: "0.68rem" }}>
        Backfill
      </Typography>
      <Box sx={{ display: "grid", gridTemplateColumns: "repeat(4, 1fr)", gap: 2, mt: 1, mb: 3 }}>
        <StatCard
          label="Backfilling Addresses"
          value={s?.backfillingAddressCount ?? 0}
          accent={s && s.backfillingAddressCount > 0 ? "info" : "default"}
        />
        <StatCard
          label="Backfilling Tokens"
          value={s?.backfillingTokenCount ?? 0}
          accent={s && s.backfillingTokenCount > 0 ? "info" : "default"}
        />
        <StatCard
          label="Full History Live — Addr"
          value={s?.fullHistoryLiveAddressCount ?? 0}
          accent={s && s.fullHistoryLiveAddressCount > 0 ? "success" : "default"}
        />
        <StatCard
          label="Full History Live — Token"
          value={s?.fullHistoryLiveTokenCount ?? 0}
          accent={s && s.fullHistoryLiveTokenCount > 0 ? "success" : "default"}
        />
      </Box>

      {/* Infrastructure status */}
      <Typography variant="overline" sx={{ color: "text.disabled", fontSize: "0.68rem" }}>
        Infrastructure
      </Typography>
      <Box sx={{ display: "grid", gridTemplateColumns: "repeat(3, 1fr)", gap: 2, mt: 1 }}>
        <JsonPanel title="Sync Status" data={store.syncStatus} />
        <JsonPanel title="Cache Status" data={store.cacheStatus} />
        <JsonPanel title="Storage Status" data={store.storageStatus} />
      </Box>
    </Box>
  );
});
