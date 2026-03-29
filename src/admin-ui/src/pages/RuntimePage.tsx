import { observer } from "mobx-react-lite";
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  IconButton,
  Skeleton,
  Stack,
  Tooltip,
  Typography,
} from "@mui/material";
import RefreshOutlinedIcon from "@mui/icons-material/RefreshOutlined";
import { JsonPanel } from "@/components/JsonPanel";
import { opsStore } from "@/stores/ops.store";

export const RuntimePage = observer(function RuntimePage() {
  const store = opsStore;

  return (
    <Box>
      <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", mb: 3 }}>
        <Box>
          <Typography variant="h5" sx={{ fontWeight: 600, letterSpacing: "-0.02em" }}>
            Runtime / Ops
          </Typography>
          <Typography variant="body2" sx={{ color: "text.secondary", mt: 0.5 }}>
            Diagnostics-only surface. Provider onboarding and configuration now live on the dedicated Providers page.
          </Typography>
        </Box>
        <Tooltip title="Refresh">
          <IconButton
            size="small"
            onClick={() => store.reload()}
            disabled={store.isLoading || store.refreshing}
            sx={{ color: "text.disabled" }}
          >
            {store.refreshing ? <CircularProgress size={16} color="inherit" /> : <RefreshOutlinedIcon fontSize="small" />}
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
        <Stack spacing={2}>
          <Skeleton variant="rounded" height={56} />
          <Skeleton variant="rounded" height={56} />
          <Skeleton variant="rounded" height={56} />
        </Stack>
      ) : (
        <Stack spacing={2}>
          <JsonPanel title="Cache Status (admin)" data={store.adminCacheStatus} />
          <JsonPanel title="Providers (ops)" data={store.providers} />
          <JsonPanel title="Cache Detail (ops)" data={store.opsCache} />
        </Stack>
      )}
    </Box>
  );
});
