import { Alert, Box, Button, Chip, Dialog, DialogContent, DialogTitle, IconButton, MenuItem, Paper, Stack, TextField, Typography } from "@mui/material";
import CloseIcon from "@mui/icons-material/Close";
import RefreshIcon from "@mui/icons-material/Refresh";
import { DataGrid, type GridColDef } from "@mui/x-data-grid";
import { observer } from "mobx-react-lite";
import { useEffect, useMemo, useState } from "react";
import type { IAdminClient } from "@/lib/admin/admin-client";
import type { AdminAuditLogEntryResponse } from "@/types/admin";
import { AuditLogStore } from "@/screens/audit-log/audit-log.store";

/**
 * wave-A3 S3 — read-only forensic record of destructive admin
 * operations. The page is intentionally simple: filter chips
 * at the top, virtualised DataGrid below, click-to-inspect
 * dialog for the JSON context.
 *
 * No mutate / delete affordance — once a row lands here, it
 * stays read-only forever (the slice contract).
 */
export const AuditLogPage = observer(function AuditLogPage({ admin }: { admin: IAdminClient }) {
  const store = useMemo(() => new AuditLogStore({ admin }), [admin]);
  const [selected, setSelected] = useState<AdminAuditLogEntryResponse | null>(null);

  useEffect(() => {
    void store.refresh();
    return () => store.dispose();
  }, [store]);

  const columns = useMemo<GridColDef<AdminAuditLogEntryResponse>[]>(() => [
    {
      field: "unixMs",
      headerName: "When (UTC)",
      width: 200,
      valueFormatter: (value: number) =>
        new Date(value).toISOString().replace("T", " ").slice(0, 19),
    },
    { field: "username", headerName: "User", width: 140 },
    {
      field: "action",
      headerName: "Action",
      width: 160,
      renderCell: (params) => (
        <Chip size="small" label={params.row.action} variant="outlined" />
      ),
    },
    {
      field: "targetId",
      headerName: "Target",
      flex: 1,
      minWidth: 240,
      renderCell: (params) => (
        <Typography variant="body2" fontFamily="monospace" noWrap title={params.row.targetId}>
          {params.row.targetId}
        </Typography>
      ),
    },
    {
      field: "context",
      headerName: "Context",
      width: 140,
      renderCell: (params) => (
        <Button
          size="small"
          variant="text"
          disabled={!params.row.context}
          onClick={() => setSelected(params.row)}
        >
          {params.row.context ? "View JSON" : "—"}
        </Button>
      ),
      sortable: false,
    },
  ], []);

  return (
    <Stack spacing={2} sx={{ p: 2 }}>
      <Stack direction="row" alignItems="baseline" spacing={2}>
        <Typography variant="h5">Audit Log</Typography>
        <Typography variant="body2" color="text.secondary">
          Read-only forensic record of destructive operations.
        </Typography>
      </Stack>

      <Paper variant="outlined" sx={{ p: 2 }}>
        <Stack direction={{ xs: "column", md: "row" }} spacing={2}>
          <TextField
            label="Action"
            size="small"
            select
            value={store.filter.action}
            onChange={(e) => store.setActionFilter(e.target.value)}
            sx={{ minWidth: 180 }}
          >
            <MenuItem value="">All</MenuItem>
            <MenuItem value="broadcast_tx">broadcast_tx</MenuItem>
          </TextField>
          <TextField
            label="User"
            size="small"
            value={store.filter.username}
            onChange={(e) => store.setUsernameFilter(e.target.value)}
            sx={{ minWidth: 160 }}
          />
          <TextField
            label="Since (ISO-8601 or unix-ms)"
            size="small"
            value={store.filter.since}
            onChange={(e) => store.setSinceFilter(e.target.value)}
            sx={{ minWidth: 240 }}
          />
          <Button
            startIcon={<RefreshIcon />}
            variant="outlined"
            onClick={() => void store.refresh()}
            disabled={store.status === "loading"}
          >
            Refresh
          </Button>
        </Stack>
      </Paper>

      {store.status === "error" && store.error && (
        <Alert severity="error">{store.error}</Alert>
      )}

      <Paper variant="outlined" sx={{ height: 560 }}>
        <DataGrid<AdminAuditLogEntryResponse>
          rows={store.entries}
          columns={columns}
          getRowId={(row) => row.id}
          loading={store.status === "loading"}
          density="compact"
          disableRowSelectionOnClick
          initialState={{ pagination: { paginationModel: { pageSize: 50 } } }}
          pageSizeOptions={[25, 50, 100]}
        />
      </Paper>

      <Typography variant="caption" color="text.secondary">
        {store.totalMatched.toLocaleString()} match
        {store.totalMatched === 1 ? "" : "es"}
        {store.entries.length < store.totalMatched
          ? ` · showing latest ${store.entries.length}`
          : ""}
      </Typography>

      <Dialog open={!!selected} onClose={() => setSelected(null)} fullWidth maxWidth="sm">
        <DialogTitle>
          Context
          <IconButton
            onClick={() => setSelected(null)}
            sx={{ position: "absolute", right: 8, top: 8 }}
          >
            <CloseIcon />
          </IconButton>
        </DialogTitle>
        <DialogContent dividers>
          <Box
            component="pre"
            sx={{ fontSize: 13, m: 0, whiteSpace: "pre-wrap", wordBreak: "break-word" }}
          >
            {prettyContext(selected?.context)}
          </Box>
        </DialogContent>
      </Dialog>
    </Stack>
  );
});

function prettyContext(raw: string | null | undefined): string {
  if (!raw) return "(empty)";
  try {
    return JSON.stringify(JSON.parse(raw), null, 2);
  } catch {
    return raw;
  }
}
