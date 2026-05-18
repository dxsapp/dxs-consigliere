import {
  Alert,
  Card,
  CardContent,
  CardHeader,
  Chip,
  Grid,
  LinearProgress,
  Stack,
  Typography,
} from "@mui/material";
import { DataGrid, type GridColDef } from "@mui/x-data-grid";
import { observer } from "mobx-react-lite";
import { lazy, Suspense, useEffect, useMemo } from "react";
import { ScoreBar } from "@/screens/p2p/ScoreBar";
import { P2pStore, type ScoredPeer } from "@/screens/p2p/p2p.store";
import type { IAdminClient } from "@/lib/admin/admin-client";

const SubnetDonut = lazy(() =>
  import("@/screens/p2p/SubnetDonut").then((m) => ({ default: m.SubnetDonut }))
);

const COLUMNS: GridColDef<ScoredPeer>[] = [
  {
    field: "endpoint",
    headerName: "Endpoint",
    minWidth: 180,
    flex: 1,
  },
  {
    field: "source",
    headerName: "Source",
    width: 110,
  },
  {
    field: "subnet24",
    headerName: "/24",
    width: 140,
  },
  {
    field: "score",
    headerName: "Score",
    sortable: false,
    minWidth: 220,
    flex: 1,
    valueGetter: (_value, row) => row.score.composite,
    renderCell: (params) => <ScoreBar score={params.row.score} />,
  },
  {
    field: "successCount",
    headerName: "ok",
    width: 70,
    type: "number",
  },
  {
    field: "failCount",
    headerName: "fail",
    width: 70,
    type: "number",
  },
  {
    field: "lastSeen",
    headerName: "Last seen",
    width: 200,
    valueFormatter: (value: string | null) =>
      value ? new Date(value).toUTCString().slice(5, 25) + " UTC" : "—",
  },
];

export const P2pPage = observer(function P2pPage({
  admin,
}: {
  admin: IAdminClient;
}) {
  const store = useMemo(() => new P2pStore({ admin }), [admin]);
  useEffect(() => {
    void store.start();
    return () => store.dispose();
  }, [store]);

  const peers = store.scoredPeers;
  const subnets = store.subnetBreakdown;

  return (
    <Stack spacing={3}>
      <Stack direction="row" alignItems="baseline" spacing={2} flexWrap="wrap" useFlexGap>
        <Typography variant="h4">P2P Pool</Typography>
        <Chip label="S8" size="small" variant="outlined" />
        {store.health && (
          <>
            <Chip
              label={`Pool ${store.health.poolSize}/${store.health.targetPoolSize}`}
              size="small"
              color={
                store.health.poolSize >= store.health.targetPoolSize
                  ? "success"
                  : "warning"
              }
            />
            <Chip
              label={`/24 diversity ${store.health.subnet24Diversity}`}
              size="small"
              variant="outlined"
            />
          </>
        )}
      </Stack>

      {store.status === "loading" && !store.health && <LinearProgress />}
      {store.error && (
        <Alert severity="error">P2P diagnostics failed: {store.error}</Alert>
      )}

      <Grid container spacing={3}>
        <Grid size={{ xs: 12, md: 8 }}>
          <Card sx={{ height: "100%" }}>
            <CardHeader
              title="Peers"
              subheader={`${peers.length} total · ${store.peers?.successful ?? 0} successful · ${store.peers?.failed ?? 0} failed`}
            />
            <CardContent>
              <DataGrid
                autoHeight
                density="compact"
                rows={peers}
                columns={COLUMNS}
                getRowId={(row) => row.endpoint}
                disableRowSelectionOnClick
                pageSizeOptions={[10, 25, 50]}
                initialState={{ pagination: { paginationModel: { pageSize: 25 } } }}
                data-testid="p2p-peers-grid"
                getRowHeight={() => 72}
              />
            </CardContent>
          </Card>
        </Grid>
        <Grid size={{ xs: 12, md: 4 }}>
          <Card sx={{ height: "100%" }}>
            <CardHeader
              title="/24 diversity"
              subheader={`${subnets.length} distinct subnets`}
            />
            <CardContent>
              <Suspense fallback={<LinearProgress />}>
                <SubnetDonut subnets={subnets} />
              </Suspense>
            </CardContent>
          </Card>
        </Grid>
      </Grid>
    </Stack>
  );
});
