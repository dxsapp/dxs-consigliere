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
  Collapse,
} from "@mui/material";
import AddOutlinedIcon from "@mui/icons-material/AddOutlined";
import { DataGrid } from "@mui/x-data-grid";
import type { GridColDef } from "@mui/x-data-grid";
import { tokenListStore } from "@/stores/token-list.store";
import { notifyStore } from "@/stores/notify.store";
import { ReadinessChip } from "@/components/ReadinessChip";
import { TrustedRootsInput, parseTrustedRoots } from "@/components/TrustedRootsInput";
import type { TrackedTokenListItem, HistoryPolicyMode } from "@/types/api";

function formatDate(value: number | null | undefined): string {
  if (value == null) return "—";
  return new Intl.DateTimeFormat("en-GB", { dateStyle: "medium", timeStyle: "short" }).format(
    new Date(value),
  );
}

// ─── Add Token Dialog ─────────────────────────────────────────────────────────

interface AddDialogProps {
  open: boolean;
  onClose: () => void;
}

function AddTokenDialog({ open, onClose }: AddDialogProps) {
  const [tokenId, setTokenId] = useState("");
  const [symbol, setSymbol] = useState("");
  const [mode, setMode] = useState<HistoryPolicyMode>("forward_only");
  const [rootsRaw, setRootsRaw] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [fieldError, setFieldError] = useState("");

  const showRoots = mode === "full_history";
  const { roots, invalid } = parseTrustedRoots(rootsRaw);
  const rootsInvalid = showRoots && invalid.length > 0;
  const rootsRequired = showRoots && roots.length === 0;

  const handleClose = () => {
    setTokenId("");
    setSymbol("");
    setMode("forward_only");
    setRootsRaw("");
    setFieldError("");
    onClose();
  };

  const handleSubmit = async () => {
    const trimmed = tokenId.trim();
    if (!trimmed) {
      setFieldError("Token ID is required.");
      return;
    }
    if (rootsRequired) {
      setFieldError("Trusted root txids are required for token full history.");
      return;
    }
    if (rootsInvalid) {
      setFieldError("Fix invalid trusted root entries before submitting.");
      return;
    }
    setFieldError("");
    setSubmitting(true);
    const result = await tokenListStore.add({
      tokenId: trimmed,
      symbol: symbol.trim() || undefined,
      historyPolicy: { mode },
      tokenHistoryPolicy: roots.length > 0 ? { trustedRoots: roots } : undefined,
    });
    setSubmitting(false);
    if (result.ok) {
      notifyStore.success("Token added successfully.");
      handleClose();
    } else {
      setFieldError(result.error ?? "Failed to add token.");
    }
  };

  return (
    <Dialog open={open} onClose={handleClose} maxWidth="sm" fullWidth>
      <DialogTitle>Track Token</DialogTitle>
      <DialogContent sx={{ display: "flex", flexDirection: "column", gap: 2, pt: "16px !important" }}>
        <TextField
          label="Token ID"
          value={tokenId}
          onChange={(e) => setTokenId(e.target.value)}
          fullWidth
          size="small"
          required
          error={Boolean(fieldError) && !tokenId.trim()}
          helperText={(!tokenId.trim() && fieldError) || " "}
          inputProps={{ style: { fontFamily: "monospace", fontSize: "0.8rem" } }}
          autoFocus
        />
        <TextField
          label="Symbol (optional)"
          value={symbol}
          onChange={(e) => setSymbol(e.target.value)}
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
                  <Typography variant="body2">Rooted historical backfill</Typography>
                  <Typography variant="caption" sx={{ color: "text.disabled" }}>
                    Expand trusted-root history inside the local managed scope. This may require
                    higher-capacity provider access, disk space, and long-running sync time.
                  </Typography>
                </Box>
              }
            />
          </RadioGroup>
        </FormControl>

        <Collapse in={showRoots}>
          <TrustedRootsInput
            value={rootsRaw}
            onChange={setRootsRaw}
            label="Trusted Root TxIDs"
            required
          />
        </Collapse>

        {fieldError && !(!tokenId.trim() && fieldError) && (
          <Alert severity="error" sx={{ py: 0.5 }}>
            {fieldError}
          </Alert>
        )}
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2 }}>
        <Button onClick={handleClose} color="inherit" disabled={submitting}>
          Cancel
        </Button>
        <Button
          onClick={handleSubmit}
          variant="contained"
          disabled={submitting || rootsInvalid || rootsRequired}
        >
          Add
        </Button>
      </DialogActions>
    </Dialog>
  );
}

// ─── Page ─────────────────────────────────────────────────────────────────────

export const TokensPage = observer(function TokensPage() {
  const navigate = useNavigate();
  const [addOpen, setAddOpen] = useState(false);

  const columns: GridColDef<TrackedTokenListItem>[] = [
    {
      field: "tokenId",
      headerName: "Token ID",
      flex: 2,
      minWidth: 200,
      renderCell: (params) => (
        <Box
          component="span"
          onClick={() => navigate(`/tokens/${encodeURIComponent(params.row.tokenId)}`)}
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
      field: "symbol",
      headerName: "Symbol",
      width: 120,
      renderCell: (params) =>
        params.value ? (
          <Typography variant="body2" sx={{ fontWeight: 500 }}>
            {params.value as string}
          </Typography>
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
      <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", mb: 3 }}>
        <Box>
          <Typography variant="h5" sx={{ fontWeight: 600, letterSpacing: "-0.02em" }}>
            Tracked Tokens
          </Typography>
          {tokenListStore.loadState === "success" && (
            <Typography variant="body2" sx={{ color: "text.disabled", mt: 0.25 }}>
              {tokenListStore.items.length} token{tokenListStore.items.length !== 1 ? "s" : ""}
            </Typography>
          )}
        </Box>
        <Button
          variant="contained"
          size="small"
          startIcon={<AddOutlinedIcon />}
          onClick={() => setAddOpen(true)}
        >
          Add Token
        </Button>
      </Box>

      {tokenListStore.loadState === "error" && (
        <Alert
          severity="error"
          action={
            <Button color="inherit" size="small" onClick={() => tokenListStore.reload()}>
              Retry
            </Button>
          }
          sx={{ mb: 2 }}
        >
          {tokenListStore.error}
        </Alert>
      )}

      <Box sx={{ mb: 1.5, display: "flex", alignItems: "center", gap: 1 }}>
        <FormControlLabel
          control={
            <Radio
              size="small"
              checked={tokenListStore.includeTombstoned}
              onChange={(e) => {
                tokenListStore.setIncludeTombstoned(e.target.checked);
                void tokenListStore.ensureLoaded();
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

      <Box sx={{ height: "calc(100vh - 220px)", minHeight: 300 }}>
        <DataGrid
          rows={tokenListStore.items}
          columns={columns}
          getRowId={(row) => row.tokenId}
          loading={tokenListStore.isLoading}
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

      <AddTokenDialog open={addOpen} onClose={() => setAddOpen(false)} />
    </Box>
  );
});
