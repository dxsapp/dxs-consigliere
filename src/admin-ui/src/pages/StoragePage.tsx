import { observer } from "mobx-react-lite";
import {
  Box,
  Typography,
  Alert,
  Button,
  Skeleton,
  IconButton,
  Tooltip,
  CircularProgress,
} from "@mui/material";
import RefreshOutlinedIcon from "@mui/icons-material/RefreshOutlined";
import { opsStore } from "@/stores/ops.store";
import { JsonPanel } from "@/components/JsonPanel";

export const StoragePage = observer(function StoragePage() {
  const store = opsStore;

  return (
    <Box>
      <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", mb: 3 }}>
        <Typography variant="h5" sx={{ fontWeight: 600, letterSpacing: "-0.02em" }}>
          Storage / Sources
        </Typography>
        <Tooltip title="Refresh">
          <IconButton
            size="small"
            onClick={() => store.reload()}
            disabled={store.isLoading || store.refreshing}
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

      {store.isLoading && !store.refreshing ? (
        <Box sx={{ display: "flex", flexDirection: "column", gap: 2 }}>
          <Skeleton variant="rounded" height={56} />
          <Skeleton variant="rounded" height={56} />
        </Box>
      ) : (
        <Box sx={{ display: "flex", flexDirection: "column", gap: 2 }}>
          <JsonPanel title="Storage Status (admin)" data={store.adminStorageStatus} />
          <JsonPanel title="Storage Detail (ops)" data={store.opsStorage} />
        </Box>
      )}
    </Box>
  );
});
