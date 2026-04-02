import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { observer } from "mobx-react-lite";
import {
  Box,
  Typography,
  Button,
  Chip,
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  TextField,
  FormControl,
  FormLabel,
  RadioGroup,
  FormControlLabel,
  Radio,
  Alert,
  FormHelperText,
} from "@mui/material";
import AddOutlinedIcon from "@mui/icons-material/AddOutlined";
import { DataGrid } from "@mui/x-data-grid";
import type { GridColDef } from "@mui/x-data-grid";
import { addressListStore } from "@/stores/address-list.store";
import { notifyStore } from "@/stores/notify.store";
import { ReadinessChip } from "@/components/ReadinessChip";
import type { TrackedAddressListItem, HistoryPolicyMode } from "@/types/api";

// ─── Helpers ─────────────────────────────────────────────────────────────────

function formatDate(value: number | null | undefined): string {
  if (value == null) return "—";
  return new Intl.DateTimeFormat("en-GB", { dateStyle: "medium", timeStyle: "short" }).format(
    new Date(value),
  );
}

// ─── Add Address Dialog ───────────────────────────────────────────────────────

interface AddDialogProps {
  open: boolean;
  onClose: () => void;
}

function AddAddressDialog({ open, onClose }: AddDialogProps) {
  const [address, setAddress] = useState("");
  const [name, setName] = useState("");
  const [mode, setMode] = useState<HistoryPolicyMode>("forward_only");
  const [submitting, setSubmitting] = useState(false);
  const [fieldError, setFieldError] = useState("");

  const handleClose = () => {
    setAddress("");
    setName("");
    setMode("forward_only");
    setFieldError("");
    onClose();
  };

  const handleSubmit = async () => {
    const trimmed = address.trim();
    if (!trimmed) {
      setFieldError("Address is required.");
      return;
    }
    setFieldError("");
    setSubmitting(true);
    const result = await addressListStore.add({
      address: trimmed,
      name: name.trim() || undefined,
      historyPolicy: { mode },
    });
    setSubmitting(false);
    if (result.ok) {
      notifyStore.success("Address added successfully.");
      handleClose();
    } else {
      setFieldError(result.error ?? "Failed to add address.");
    }
  };

  return (
    <Dialog open={open} onClose={handleClose} maxWidth="xs" fullWidth>
      <DialogTitle>Track Address</DialogTitle>
      <DialogContent sx={{ display: "flex", flexDirection: "column", gap: 2, pt: "16px !important" }}>
        <TextField
          label="Address"
          value={address}
          onChange={(e) => setAddress(e.target.value)}
          fullWidth
          size="small"
          required
          error={Boolean(fieldError)}
          helperText={fieldError || " "}
          inputProps={{ style: { fontFamily: "monospace", fontSize: "0.8rem" } }}
          autoFocus
        />
        <TextField
          label="Name (optional)"
          value={name}
          onChange={(e) => setName(e.target.value)}
          fullWidth
          size="small"
          helperText=" "
        />
        <FormControl>
          <FormLabel sx={{ fontSize: "0.875rem", mb: 0.5 }}>History Policy</FormLabel>
          <RadioGroup value={mode} onChange={(e) => setMode(e.target.value as HistoryPolicyMode)}>
            <FormControlLabel
              value="forward_only"
              control={<Radio size="small" />}
              label={
                <Box>
                  <Typography variant="body2">Forward only</Typography>
                  <Typography variant="caption" sx={{ color: "text.disabled" }}>
                    Track new transactions from now
                  </Typography>
                </Box>
              }
            />
            <FormControlLabel
              value="full_history"
              control={<Radio size="small" />}
              label={
                <Box>
                  <Typography variant="body2">Historical backfill</Typography>
                  <Typography variant="caption" sx={{ color: "text.disabled" }}>
                    Attempt scoped historical backfill. This may require higher-capacity provider access,
                    disk space, and long-running sync time.
                  </Typography>
                </Box>
              }
            />
          </RadioGroup>
          <FormHelperText> </FormHelperText>
        </FormControl>
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2 }}>
        <Button onClick={handleClose} color="inherit" disabled={submitting}>
          Cancel
        </Button>
        <Button onClick={handleSubmit} variant="contained" disabled={submitting}>
          Add
        </Button>
      </DialogActions>
    </Dialog>
  );
}

// ─── Page ─────────────────────────────────────────────────────────────────────

export const AddressesPage = observer(function AddressesPage() {
  const navigate = useNavigate();
  const [addOpen, setAddOpen] = useState(false);

  const columns: GridColDef<TrackedAddressListItem>[] = [
    {
      field: "address",
      headerName: "Address",
      flex: 2,
      minWidth: 200,
      renderCell: (params) => (
        <Box
          component="span"
          onClick={() => navigate(`/addresses/${encodeURIComponent(params.row.address)}`)}
          sx={{
            fontFamily: "monospace",
            fontSize: "0.78rem",
            cursor: "pointer",
            color: "primary.light",
            "&:hover": { textDecoration: "underline" },
          }}
        >
          {params.value as string}
        </Box>
      ),
    },
    {
      field: "name",
      headerName: "Name",
      width: 160,
      renderCell: (params) =>
        params.value ? (
          <Typography variant="body2">{params.value as string}</Typography>
        ) : (
          <Typography variant="body2" sx={{ color: "text.disabled" }}>
            —
          </Typography>
        ),
    },
    {
      field: "stateReadiness",
      headerName: "State",
      width: 150,
      sortable: false,
      filterable: false,
      renderCell: (params) => (
        <ReadinessChip readiness={params.row.readiness.lifecycleStatus} />
      ),
    },
    {
      field: "historyReadiness",
      headerName: "History",
      width: 170,
      sortable: false,
      filterable: false,
      renderCell: (params) => (
        <ReadinessChip
          readiness={params.row.readiness.history?.historyReadiness ?? "not_requested"}
        />
      ),
    },
    {
      field: "integritySafe",
      headerName: "Integrity",
      width: 110,
      renderCell: (params) => (
        <Chip
          label={params.value ? "safe" : "unsafe"}
          color={params.value ? "success" : "error"}
          size="small"
          variant="outlined"
          sx={{ height: 22, fontSize: "0.7rem" }}
        />
      ),
    },
    {
      field: "createdAt",
      headerName: "Tracked since",
      width: 180,
      renderCell: (params) => (
        <Typography variant="body2" sx={{ color: "text.secondary", fontSize: "0.8rem" }}>
          {formatDate(params.value as number)}
        </Typography>
      ),
    },
  ];

  return (
    <Box>
      {/* Header */}
      <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", mb: 3 }}>
        <Box>
          <Typography variant="h5" sx={{ fontWeight: 600, letterSpacing: "-0.02em" }}>
            Tracked Addresses
          </Typography>
          {addressListStore.loadState === "success" && (
            <Typography variant="body2" sx={{ color: "text.disabled", mt: 0.25 }}>
          {addressListStore.items.length} address
              {addressListStore.items.length !== 1 ? "es" : ""}
            </Typography>
          )}
        </Box>
        <Button
          variant="contained"
          size="small"
          startIcon={<AddOutlinedIcon />}
          onClick={() => setAddOpen(true)}
        >
          Add Address
        </Button>
      </Box>

      {/* Error state */}
      {addressListStore.loadState === "error" && (
        <Alert
          severity="error"
          action={
            <Button color="inherit" size="small" onClick={() => addressListStore.reload()}>
              Retry
            </Button>
          }
          sx={{ mb: 2 }}
        >
          {addressListStore.error}
        </Alert>
      )}

      {/* Tombstoned toggle */}
      <Box sx={{ mb: 1.5, display: "flex", alignItems: "center", gap: 1 }}>
        <FormControlLabel
          control={
            <Radio
              size="small"
              checked={addressListStore.includeTombstoned}
              onChange={(e) => {
                addressListStore.setIncludeTombstoned(e.target.checked);
                void addressListStore.ensureLoaded();
              }}
              sx={{ p: 0.5 }}
            />
          }
          label={
            <Typography variant="body2" sx={{ color: "text.secondary" }}>
              Show tombstoned
            </Typography>
          }
        />
      </Box>

      {/* Grid */}
      <Box sx={{ height: "calc(100vh - 220px)", minHeight: 300 }}>
        <DataGrid
          rows={addressListStore.items}
          columns={columns}
          getRowId={(row) => row.address}
          loading={addressListStore.isLoading}
          density="compact"
          disableRowSelectionOnClick
          pageSizeOptions={[25, 50, 100]}
          initialState={{ pagination: { paginationModel: { pageSize: 50 } } }}
          sx={{
            border: "1px solid",
            borderColor: "divider",
            borderRadius: 2,
            "& .MuiDataGrid-columnHeader": { fontSize: "0.78rem" },
            "& .MuiDataGrid-cell": { alignItems: "center" },
          }}
        />
      </Box>

      <AddAddressDialog open={addOpen} onClose={() => setAddOpen(false)} />
    </Box>
  );
});
