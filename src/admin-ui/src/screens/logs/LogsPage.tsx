import {
  Alert,
  Box,
  Chip,
  IconButton,
  MenuItem,
  Paper,
  Stack,
  TextField,
  Typography,
} from "@mui/material";
import ClearAllIcon from "@mui/icons-material/ClearAll";
import RefreshIcon from "@mui/icons-material/Refresh";
import { observer } from "mobx-react-lite";
import { useEffect, useMemo } from "react";
import { LogsStore } from "@/screens/logs/logs.store";

/**
 * wave-A3 S4 — live tail of the backend log stream via the
 * SignalR `/ws/logs` hub. The wave-A1 paste box is gone; the
 * sanitizer logic stays in `screens/logs/sanitizer.ts` as a
 * defence-in-depth pass on the wire payload (the backend's
 * `LogSanitizer` is the primary).
 *
 * Rendering keeps the latest 1000 entries (matching the
 * backend ring) in a fixed-height scrollable Paper. We don't
 * use `react-window` yet — 1000 rows of monospace text render
 * fine without virtualisation in MUI.
 */
export const LogsPage = observer(function LogsPage() {
  const store = useMemo(() => new LogsStore(), []);

  useEffect(() => {
    void store.start();
    return () => void store.stop();
  }, [store]);

  return (
    <Stack spacing={2} sx={{ p: 2 }}>
      <Stack direction="row" alignItems="baseline" spacing={2}>
        <Typography variant="h5">Logs · live tail</Typography>
        <Chip
          size="small"
          variant="outlined"
          color={statusColor(store.status)}
          label={store.status}
        />
      </Stack>

      <Paper variant="outlined" sx={{ p: 2 }}>
        <Stack direction={{ xs: "column", md: "row" }} spacing={2}>
          <TextField
            label="Min level"
            select
            size="small"
            value={store.filter.minLevel}
            onChange={(e) => store.setMinLevel(e.target.value)}
            sx={{ minWidth: 160 }}
          >
            <MenuItem value="trace">trace</MenuItem>
            <MenuItem value="debug">debug</MenuItem>
            <MenuItem value="information">information</MenuItem>
            <MenuItem value="warning">warning</MenuItem>
            <MenuItem value="error">error</MenuItem>
            <MenuItem value="critical">critical</MenuItem>
          </TextField>
          <TextField
            label="Category filter (substring)"
            size="small"
            value={store.filter.category}
            onChange={(e) => store.setCategoryFilter(e.target.value)}
            sx={{ minWidth: 240 }}
          />
          <IconButton
            title="Re-subscribe with current filters"
            onClick={() => void store.resubscribe()}
          >
            <RefreshIcon />
          </IconButton>
          <IconButton title="Clear local view" onClick={() => store.clear()}>
            <ClearAllIcon />
          </IconButton>
        </Stack>
      </Paper>

      {store.status === "error" && store.error && (
        <Alert severity="error">{store.error}</Alert>
      )}

      <Paper
        variant="outlined"
        sx={{ height: 560, overflowY: "auto", fontFamily: "monospace", fontSize: 13 }}
      >
        <Box component="ol" sx={{ m: 0, p: 0, listStyle: "none" }}>
          {store.entries.length === 0 ? (
            <Box sx={{ p: 2, color: "text.secondary" }}>
              {store.status === "live"
                ? "Connected. Waiting for backend log emissions…"
                : "—"}
            </Box>
          ) : (
            store.entries.map((entry, idx) => (
              <Box
                component="li"
                key={`${entry.unixMs}-${idx}`}
                sx={{
                  px: 2,
                  py: 0.5,
                  borderBottom: "1px solid",
                  borderColor: "divider",
                  whiteSpace: "pre-wrap",
                  color: levelColor(entry.level),
                }}
              >
                <Box component="span" sx={{ color: "text.secondary", mr: 1 }}>
                  {new Date(entry.unixMs).toISOString().slice(11, 23)}
                </Box>
                <Box component="span" sx={{ fontWeight: 600, mr: 1 }}>
                  [{entry.level}]
                </Box>
                <Box component="span" sx={{ color: "text.secondary", mr: 1 }}>
                  {entry.category}
                </Box>
                {entry.message}
                {entry.exception && (
                  <Box component="pre" sx={{ m: 0, mt: 0.5, color: "error.main" }}>
                    {entry.exception}
                  </Box>
                )}
              </Box>
            ))
          )}
        </Box>
      </Paper>

      <Typography variant="caption" color="text.secondary">
        {store.entries.length.toLocaleString()} entries · server-side sanitizer applied; client re-runs the regex set as defence-in-depth.
      </Typography>
    </Stack>
  );
});

function statusColor(status: LogsStore["status"]): "default" | "info" | "success" | "error" | "warning" {
  switch (status) {
    case "live": return "success";
    case "connecting": return "info";
    case "error": return "error";
    case "stopped": return "warning";
    default: return "default";
  }
}

function levelColor(level: string): string | undefined {
  switch (level) {
    case "error":
    case "critical": return "error.main";
    case "warning": return "warning.main";
    case "debug":
    case "trace": return "text.secondary";
    default: return undefined;
  }
}
