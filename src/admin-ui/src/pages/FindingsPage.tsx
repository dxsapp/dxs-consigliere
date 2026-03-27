import { useNavigate } from "react-router-dom";
import { observer } from "mobx-react-lite";
import {
  Box,
  Typography,
  Button,
  Chip,
  Alert,
  ToggleButtonGroup,
  ToggleButton,
  IconButton,
  Tooltip,
  CircularProgress,
} from "@mui/material";
import RefreshOutlinedIcon from "@mui/icons-material/RefreshOutlined";
import { DataGrid } from "@mui/x-data-grid";
import type { GridColDef } from "@mui/x-data-grid";
import { findingsStore } from "@/stores/findings.store";
import type { Finding, FindingSeverity } from "@/types/api";

// ─── Helpers ─────────────────────────────────────────────────────────────────

function formatDate(iso: string): string {
  return new Intl.DateTimeFormat("en-GB", { dateStyle: "medium", timeStyle: "short" }).format(
    new Date(iso),
  );
}

function SeverityChip({ severity }: { severity: FindingSeverity }) {
  const color =
    severity === "error" ? "error" : severity === "warning" ? "warning" : "default";
  return (
    <Chip
      label={severity}
      color={color}
      size="small"
      variant="filled"
      sx={{ height: 20, fontSize: "0.68rem", fontWeight: 600 }}
    />
  );
}

// ─── Page ─────────────────────────────────────────────────────────────────────

export const FindingsPage = observer(function FindingsPage() {
  const navigate = useNavigate();

  const columns: GridColDef<Finding>[] = [
    {
      field: "severity",
      headerName: "Severity",
      width: 100,
      renderCell: (params) => <SeverityChip severity={params.value as FindingSeverity} />,
    },
    {
      field: "entityType",
      headerName: "Entity Type",
      width: 120,
      renderCell: (params) => (
        <Typography variant="body2" sx={{ fontFamily: "monospace", fontSize: "0.78rem" }}>
          {params.value as string}
        </Typography>
      ),
    },
    {
      field: "entityId",
      headerName: "Entity ID",
      width: 200,
      renderCell: (params) => {
        const val = params.value as string;
        const isAddress = params.row.entityType === "address" || params.row.entityType === "token";
        const path =
          params.row.entityType === "address"
            ? `/addresses/${encodeURIComponent(val)}`
            : params.row.entityType === "token"
              ? `/tokens/${encodeURIComponent(val)}`
              : null;
        return path ? (
          <Box
            component="span"
            onClick={() => navigate(path)}
            sx={{
              fontFamily: "monospace",
              fontSize: "0.78rem",
              cursor: "pointer",
              color: "primary.light",
              "&:hover": { textDecoration: "underline" },
            }}
          >
            {val}
          </Box>
        ) : (
          <Typography
            variant="body2"
            sx={{ fontFamily: "monospace", fontSize: "0.78rem" }}
            title={isAddress ? val : undefined}
          >
            {val.length > 24 ? `${val.slice(0, 10)}…${val.slice(-8)}` : val}
          </Typography>
        );
      },
    },
    {
      field: "code",
      headerName: "Code",
      width: 200,
      renderCell: (params) => (
        <Typography variant="body2" sx={{ fontFamily: "monospace", fontSize: "0.78rem", color: "text.secondary" }}>
          {params.value as string}
        </Typography>
      ),
    },
    {
      field: "message",
      headerName: "Message",
      flex: 1,
      minWidth: 200,
      renderCell: (params) => (
        <Typography variant="body2" sx={{ fontSize: "0.8rem" }} title={params.value as string}>
          {params.value as string}
        </Typography>
      ),
    },
    {
      field: "observedAt",
      headerName: "Observed",
      width: 180,
      renderCell: (params) => (
        <Typography variant="body2" sx={{ color: "text.secondary", fontSize: "0.8rem" }}>
          {formatDate(params.value as string)}
        </Typography>
      ),
    },
  ];

  const store = findingsStore;
  const refreshing = store.isLoading && store.items.length > 0;

  return (
    <Box>
      {/* Header */}
      <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", mb: 3 }}>
        <Box>
          <Typography variant="h5" sx={{ fontWeight: 600, letterSpacing: "-0.02em" }}>
            Findings
          </Typography>
          {store.loadState === "success" && (
            <Typography variant="body2" sx={{ color: "text.disabled", mt: 0.25 }}>
              {store.items.length} total
              {store.errorCount > 0 && (
                <Box component="span" sx={{ color: "error.main", ml: 1 }}>
                  {store.errorCount} error{store.errorCount !== 1 ? "s" : ""}
                </Box>
              )}
              {store.warningCount > 0 && (
                <Box component="span" sx={{ color: "warning.main", ml: 1 }}>
                  {store.warningCount} warning{store.warningCount !== 1 ? "s" : ""}
                </Box>
              )}
            </Typography>
          )}
        </Box>
        <Tooltip title="Refresh">
          <IconButton
            size="small"
            onClick={() => store.reload()}
            disabled={store.isLoading}
            sx={{ color: "text.disabled" }}
          >
            {refreshing ? (
              <CircularProgress size={16} color="inherit" />
            ) : (
              <RefreshOutlinedIcon fontSize="small" />
            )}
          </IconButton>
        </Tooltip>
      </Box>

      {/* Error */}
      {store.loadState === "error" && (
        <Alert
          severity="error"
          action={
            <Button color="inherit" size="small" onClick={() => store.reload()}>
              Retry
            </Button>
          }
          sx={{ mb: 2 }}
        >
          {store.error}
        </Alert>
      )}

      {/* Severity filter */}
      <Box sx={{ mb: 1.5 }}>
        <ToggleButtonGroup
          value={store.severityFilter}
          exclusive
          size="small"
          onChange={(_, val) => { if (val) store.setSeverityFilter(val); }}
          sx={{ "& .MuiToggleButton-root": { py: 0.4, px: 1.5, fontSize: "0.75rem" } }}
        >
          <ToggleButton value="all">All ({store.items.length})</ToggleButton>
          <ToggleButton value="error" sx={{ "&.Mui-selected": { color: "error.main", borderColor: "error.main" } }}>
            Errors ({store.errorCount})
          </ToggleButton>
          <ToggleButton value="warning" sx={{ "&.Mui-selected": { color: "warning.main", borderColor: "warning.main" } }}>
            Warnings ({store.warningCount})
          </ToggleButton>
        </ToggleButtonGroup>
      </Box>

      {/* Grid */}
      <Box sx={{ height: "calc(100vh - 240px)", minHeight: 300 }}>
        <DataGrid
          rows={store.filteredItems}
          columns={columns}
          getRowId={(row) =>
            `${row.entityType}:${row.entityId}:${row.code}:${row.observedAt}`
          }
          loading={store.isLoading && store.items.length === 0}
          density="compact"
          disableRowSelectionOnClick
          pageSizeOptions={[25, 50, 100]}
          initialState={{
            pagination: { paginationModel: { pageSize: 50 } },
            sorting: { sortModel: [{ field: "observedAt", sort: "desc" }] },
          }}
          sx={{
            border: "1px solid",
            borderColor: "divider",
            borderRadius: 2,
            "& .MuiDataGrid-columnHeader": { fontSize: "0.78rem" },
            "& .MuiDataGrid-cell": { alignItems: "center" },
          }}
        />
      </Box>
    </Box>
  );
});
