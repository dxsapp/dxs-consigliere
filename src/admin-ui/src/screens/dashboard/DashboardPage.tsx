import {
  Alert,
  Card,
  CardContent,
  CardHeader,
  Chip,
  Divider,
  Grid,
  InputAdornment,
  LinearProgress,
  Stack,
  TextField,
  Typography,
  useTheme,
} from "@mui/material";
import CheckCircleIcon from "@mui/icons-material/CheckCircle";
import ErrorIcon from "@mui/icons-material/Error";
import HelpIcon from "@mui/icons-material/Help";
import HistoryIcon from "@mui/icons-material/History";
import SearchIcon from "@mui/icons-material/Search";
import WarningAmberIcon from "@mui/icons-material/WarningAmber";
import { observer } from "mobx-react-lite";
import { useEffect, useMemo, useState, type KeyboardEvent } from "react";
import { useNavigate } from "react-router-dom";
import { Sparkline } from "@/screens/dashboard/components/Sparkline";
import { DashboardStore } from "@/screens/dashboard/dashboard.store";
import {
  entityToPath,
  parseSearchQuery,
} from "@/lib/search/grammar";
import type { IAdminClient } from "@/lib/admin/admin-client";
import type { EventBus } from "@/lib/events/bus";
import { SOURCE_KEYS } from "@/types/admin";

/**
 * S4 — operator-facing landing screen. Per design brief §5.1:
 *  - composite hero: status indicator + mempool-rate sparkline
 *  - centered search prompt + recent-lookup chips
 *  - activity stream: recent broadcasts (SignalR-driven) + per-source
 *    visibility feed (poll-driven)
 *  - 1-2 inline sparklines (mempool rate, pool size over time)
 */
export const DashboardPage = observer(function DashboardPage({
  admin,
  bus,
}: {
  admin: IAdminClient;
  bus: EventBus;
}) {
  const store = useMemo(
    () => new DashboardStore({ admin, bus }),
    [admin, bus]
  );

  useEffect(() => {
    void store.start();
    return () => store.dispose();
  }, [store]);

  return (
    <Stack spacing={3}>
      <HeroCard store={store} />
      <SearchPrompt />
      <Grid container spacing={3}>
        <Grid size={{ xs: 12, md: 7 }}>
          <RecentBroadcasts store={store} />
        </Grid>
        <Grid size={{ xs: 12, md: 5 }}>
          <SourceVisibilityFeed store={store} />
        </Grid>
      </Grid>
    </Stack>
  );
});

// ── Hero ─────────────────────────────────────────────────────────

const HeroCard = observer(function HeroCard({ store }: { store: DashboardStore }) {
  const theme = useTheme();
  const { status, label } = store.healthSummary;
  const meta = {
    online: {
      color: theme.palette.severity.success.main,
      icon: <CheckCircleIcon fontSize="large" sx={{ color: theme.palette.severity.success.main }} />,
      verdict: "System healthy",
    },
    degraded: {
      color: theme.palette.severity.warning.main,
      icon: <WarningAmberIcon fontSize="large" sx={{ color: theme.palette.severity.warning.main }} />,
      verdict: "System degraded",
    },
    offline: {
      color: theme.palette.severity.error.main,
      icon: <ErrorIcon fontSize="large" sx={{ color: theme.palette.severity.error.main }} />,
      verdict: "System offline",
    },
    unknown: {
      color: theme.palette.text.disabled,
      icon: <HelpIcon fontSize="large" sx={{ color: theme.palette.text.disabled }} />,
      verdict: "Status unknown",
    },
  }[status];

  const series = store.mempoolRateSeries;

  return (
    <Card>
      <CardContent>
        <Stack direction={{ xs: "column", md: "row" }} spacing={3} alignItems={{ md: "center" }}>
          <Stack direction="row" spacing={2} alignItems="center" sx={{ flexShrink: 0 }}>
            {meta.icon}
            <Stack>
              <Typography variant="overline" color="text.secondary">
                Status
              </Typography>
              <Typography variant="h4" sx={{ color: meta.color }}>
                {meta.verdict}
              </Typography>
              <Typography variant="body2" color="text.secondary">
                {label}
              </Typography>
            </Stack>
          </Stack>

          <Divider
            orientation="vertical"
            flexItem
            sx={{ display: { xs: "none", md: "block" } }}
          />

          <Stack sx={{ flex: 1, minWidth: 0 }}>
            <Stack direction="row" justifyContent="space-between" alignItems="baseline">
              <Typography variant="overline" color="text.secondary">
                Mempool · first-seen Δ per tick
              </Typography>
              <Typography variant="caption" color="text.secondary">
                {series.length > 0 ? `${series.length} samples` : "no data yet"}
              </Typography>
            </Stack>
            <Sparkline data={series} width={420} height={56} ariaLabel="mempool first-seen sparkline" />
          </Stack>
        </Stack>

        {(store.healthStatus === "loading" || store.metricsStatus === "loading") && !store.health && (
          <LinearProgress sx={{ mt: 2 }} />
        )}

        {store.lastError && (
          <Alert severity="warning" sx={{ mt: 2 }}>
            Backend slow / unreachable: {store.lastError}
          </Alert>
        )}
      </CardContent>
    </Card>
  );
});

// ── Search prompt + recent lookups ───────────────────────────────

const RECENT_KEY = "consigliere-admin/recent-lookups";
const RECENT_LIMIT = 6;

function loadRecent(): string[] {
  if (typeof window === "undefined") return [];
  try {
    const raw = window.localStorage.getItem(RECENT_KEY);
    if (!raw) return [];
    const parsed = JSON.parse(raw);
    return Array.isArray(parsed) ? parsed.filter((x) => typeof x === "string") : [];
  } catch {
    return [];
  }
}

function persistRecent(items: string[]) {
  if (typeof window === "undefined") return;
  try {
    window.localStorage.setItem(RECENT_KEY, JSON.stringify(items.slice(0, RECENT_LIMIT)));
  } catch {
    /* swallow */
  }
}

function SearchPrompt() {
  const navigate = useNavigate();
  const [raw, setRaw] = useState("");
  const [recent, setRecent] = useState<string[]>(loadRecent);

  const onSubmit = (value: string) => {
    const trimmed = value.trim();
    if (!trimmed) return;
    const result = parseSearchQuery(trimmed);
    if (result.kind === "empty" || result.kind === "ambiguous") return;
    const path = entityToPath(result);
    if (!path) return;
    const next = [trimmed, ...recent.filter((r) => r !== trimmed)].slice(0, RECENT_LIMIT);
    setRecent(next);
    persistRecent(next);
    navigate(path);
  };

  const onKeyDown = (e: KeyboardEvent<HTMLInputElement>) => {
    if (e.key === "Enter") onSubmit(raw);
  };

  return (
    <Card>
      <CardContent>
        <Stack spacing={1.5}>
          <Typography variant="overline" color="text.secondary">
            Quick lookup
          </Typography>
          <TextField
            placeholder="Search tx / address / token / block…"
            value={raw}
            onChange={(e) => setRaw(e.target.value)}
            onKeyDown={onKeyDown}
            fullWidth
            size="small"
            InputProps={{
              startAdornment: (
                <InputAdornment position="start">
                  <SearchIcon fontSize="small" />
                </InputAdornment>
              ),
            }}
          />
          {recent.length > 0 && (
            <Stack direction="row" spacing={1} useFlexGap flexWrap="wrap">
              <Stack direction="row" alignItems="center" spacing={0.5}>
                <HistoryIcon fontSize="small" color="disabled" />
                <Typography variant="caption" color="text.secondary">
                  recent
                </Typography>
              </Stack>
              {recent.map((r) => (
                <Chip
                  key={r}
                  label={r.length > 22 ? `${r.slice(0, 22)}…` : r}
                  size="small"
                  variant="outlined"
                  onClick={() => onSubmit(r)}
                />
              ))}
            </Stack>
          )}
        </Stack>
      </CardContent>
    </Card>
  );
}

// ── Activity stream — recent broadcasts ──────────────────────────

const RecentBroadcasts = observer(function RecentBroadcasts({
  store,
}: {
  store: DashboardStore;
}) {
  return (
    <Card sx={{ height: "100%" }}>
      <CardHeader title="Recent broadcasts" subheader="live · OnBroadcastStateChanged" />
      <CardContent>
        {store.recentBroadcasts.length === 0 ? (
          <Typography variant="body2" color="text.secondary">
            No outgoing tx state transitions yet. New broadcasts appear here as the
            backend emits OnBroadcastStateChanged.
          </Typography>
        ) : (
          <Stack divider={<Divider flexItem />}>
            {store.recentBroadcasts.map((b) => (
              <Stack
                key={b.txId + b.updatedAtMs}
                direction="row"
                justifyContent="space-between"
                alignItems="center"
                sx={{ py: 1 }}
              >
                <Stack sx={{ minWidth: 0 }}>
                  <Typography variant="code" sx={{ wordBreak: "break-all" }}>
                    {b.txId.length > 24 ? `${b.txId.slice(0, 12)}…${b.txId.slice(-8)}` : b.txId}
                  </Typography>
                  <Typography variant="caption" color="text.secondary">
                    {new Date(b.updatedAtMs).toUTCString().slice(17, 25)} UTC
                    {b.failReason ? ` · ${b.failReason}` : ""}
                  </Typography>
                </Stack>
                <Chip
                  size="small"
                  label={b.state}
                  color={chipColorForState(b.state)}
                  variant={b.state === "Mined" ? "filled" : "outlined"}
                />
              </Stack>
            ))}
          </Stack>
        )}
      </CardContent>
    </Card>
  );
});

function chipColorForState(state: string): "default" | "primary" | "success" | "warning" | "error" {
  switch (state) {
    case "Validated":
    case "Dispatching":
    case "PeerRelayed":
      return "primary";
    case "Mined":
    case "Confirmed":
      return "success";
    case "PolicyInvalid":
      return "warning";
    case "Failed":
      return "error";
    default:
      return "default";
  }
}

// ── Source visibility feed ───────────────────────────────────────

const SourceVisibilityFeed = observer(function SourceVisibilityFeed({
  store,
}: {
  store: DashboardStore;
}) {
  const rates = store.sourceRates;
  return (
    <Card sx={{ height: "100%" }}>
      <CardHeader title="Source visibility" subheader="first-seen rate per minute" />
      <CardContent>
        {rates.length === 0 ? (
          <Typography variant="body2" color="text.secondary">
            Waiting for at least 2 snapshots to compute per-source rates.
          </Typography>
        ) : (
          <Stack divider={<Divider flexItem />}>
            {rates.map(({ source, ratePerMinute }) => (
              <Stack
                key={source}
                direction="row"
                justifyContent="space-between"
                alignItems="center"
                sx={{ py: 1 }}
              >
                <Stack>
                  <Typography variant="body2">{labelForSource(source)}</Typography>
                  <Typography variant="caption" color="text.secondary">
                    {source}
                  </Typography>
                </Stack>
                <Stack direction="row" alignItems="baseline" spacing={1}>
                  <Typography variant="h6">{ratePerMinute.toFixed(1)}</Typography>
                  <Typography variant="caption" color="text.secondary">
                    tx/min
                  </Typography>
                </Stack>
              </Stack>
            ))}
          </Stack>
        )}
        {SOURCE_KEYS.every((k) => rates.find((r) => r.source === k)?.ratePerMinute === 0) &&
          rates.length > 0 && (
            <Alert severity="warning" sx={{ mt: 2 }}>
              All three sources reported zero new first-seen events in the
              window. Cross-check the Alerts screen for a dropout.
            </Alert>
          )}
      </CardContent>
    </Card>
  );
});

function labelForSource(source: string): string {
  switch (source) {
    case "p2p":
      return "BSV P2P";
    case "bitails":
      return "Bitails";
    case "junglebus":
      return "JungleBus";
    default:
      return source;
  }
}
