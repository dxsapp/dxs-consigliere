import { Chip, Stack } from "@mui/material";
import { DataGrid, type GridColDef } from "@mui/x-data-grid";
import type { P2pAlertEventDto } from "@/types/admin";

/**
 * S7 — DataGrid for the older / acknowledged alert history. Lazy-
 * imported by AlertsPage so the X DataGrid bundle (≈ 90 KB gzip)
 * lands in its own chunk per S3-audit M6.
 */
const COLUMNS: GridColDef<P2pAlertEventDto>[] = [
  {
    field: "alertUnixMs",
    headerName: "When",
    width: 180,
    valueFormatter: (value: number) =>
      new Date(value).toUTCString().slice(5, 25) + " UTC",
  },
  {
    field: "type",
    headerName: "Type",
    width: 220,
    renderCell: (params) => (
      <Chip
        size="small"
        label={params.row.type}
        color={severityColor(params.row.type)}
      />
    ),
  },
  { field: "detail", headerName: "Detail", flex: 1, minWidth: 240 },
  {
    field: "context",
    headerName: "Context",
    flex: 1,
    minWidth: 180,
    renderCell: (params) => (
      <Stack direction="row" spacing={0.5} useFlexGap flexWrap="wrap">
        {Object.entries(params.row.context).map(([k, v]) => (
          <Chip key={k} size="small" label={`${k}=${v}`} variant="outlined" />
        ))}
      </Stack>
    ),
    sortable: false,
  },
];

function severityColor(type: string): "error" | "warning" | "default" {
  switch (type) {
    case "PoolSizeBelowThreshold":
    case "ReorgDepthExceeded":
      return "error";
    case "SourceFirstDropout":
    case "RelayBackRateBelowThreshold":
      return "warning";
    default:
      return "default";
  }
}

export function AlertHistoryGrid({ alerts }: { alerts: P2pAlertEventDto[] }) {
  return (
    <DataGrid
      autoHeight
      density="compact"
      rows={alerts}
      columns={COLUMNS}
      getRowId={(row) => row.id}
      disableRowSelectionOnClick
      pageSizeOptions={[10, 25, 50]}
      initialState={{ pagination: { paginationModel: { pageSize: 10 } } }}
      data-testid="alert-history-grid"
    />
  );
}
