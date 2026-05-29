import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  CardHeader,
  Chip,
  LinearProgress,
  Link,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  TextField,
  Typography,
} from "@mui/material";
import { observer } from "mobx-react-lite";
import { useEffect, useMemo } from "react";
import { Link as RouterLink } from "react-router-dom";
import { AddressesStore } from "@/screens/addresses/addresses.store";
import type { IAdminClient } from "@/lib/admin/admin-client";

/**
 * Track-a-new — Addresses. Lists currently-tracked addresses and lets
 * an operator register a new one so the P2P mempool watchlist filter can
 * be exercised. Render shell only; lifecycle owned by `AddressesStore`.
 *
 * History mode: the add form tracks `forward_only` (see the store) — the
 * right default for filter testing. No selector is exposed.
 */
export const AddressesPage = observer(function AddressesPage({
  admin,
}: {
  admin: IAdminClient;
}) {
  const store = useMemo(() => new AddressesStore({ admin }), [admin]);
  useEffect(() => {
    void store.start();
    return () => store.dispose();
  }, [store]);

  return (
    <Stack spacing={3}>
      <Stack direction="row" alignItems="baseline" spacing={2} flexWrap="wrap" useFlexGap>
        <Typography variant="h4">Addresses</Typography>
        <Chip label={`${store.addresses.length} tracked`} size="small" variant="outlined" />
      </Stack>

      <Card>
        <CardHeader
          title="Track a new address"
          subheader="Starts watching forward-only (mempool + new blocks)."
        />
        <CardContent>
          <Box
            component="form"
            onSubmit={(e) => {
              e.preventDefault();
              void store.submit();
            }}
          >
            <Stack direction={{ xs: "column", sm: "row" }} spacing={2} alignItems="flex-start">
              <TextField
                label="Address"
                value={store.formAddress}
                onChange={(e) => store.setFormAddress(e.target.value)}
                size="small"
                fullWidth
                required
                inputProps={{ "data-testid": "address-input" }}
              />
              <TextField
                label="Name (optional)"
                value={store.formName}
                onChange={(e) => store.setFormName(e.target.value)}
                size="small"
                fullWidth
                inputProps={{ "data-testid": "address-name-input" }}
              />
              <Button
                type="submit"
                variant="contained"
                disabled={!store.canSubmit}
                sx={{ minWidth: 120, mt: { xs: 0, sm: 0 } }}
                data-testid="address-submit"
              >
                {store.submitting ? "Adding…" : "Track"}
              </Button>
            </Stack>
          </Box>
          {store.submitError && (
            <Alert severity="error" sx={{ mt: 2 }} data-testid="address-submit-error">
              Could not track address: {store.submitError}
            </Alert>
          )}
        </CardContent>
      </Card>

      {store.status === "loading" && !store.addresses.length && <LinearProgress />}
      {store.error && (
        <Alert severity="error">Failed to load tracked addresses: {store.error}</Alert>
      )}

      <Card>
        <CardHeader title="Tracked addresses" />
        <CardContent>
          {store.addresses.length === 0 && store.status === "ready" ? (
            <Typography variant="body2" color="text.secondary">
              No tracked addresses yet.
            </Typography>
          ) : (
            <Table size="small" data-testid="addresses-table">
              <TableHead>
                <TableRow>
                  <TableCell>Address</TableCell>
                  <TableCell>Name</TableCell>
                  <TableCell>Status</TableCell>
                  <TableCell>Readable</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {store.addresses.map((a) => (
                  <TableRow key={a.address} hover>
                    <TableCell>
                      <Link
                        component={RouterLink}
                        to={`/addresses/${encodeURIComponent(a.address)}`}
                        sx={{ fontFamily: "monospace", wordBreak: "break-all" }}
                      >
                        {a.address}
                      </Link>
                    </TableCell>
                    <TableCell>{a.name || "—"}</TableCell>
                    <TableCell>
                      <Chip
                        size="small"
                        label={a.readiness.lifecycleStatus}
                        color={a.readiness.authoritative ? "success" : "default"}
                        variant="outlined"
                      />
                    </TableCell>
                    <TableCell>{a.readiness.readable ? "yes" : "no"}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>
    </Stack>
  );
});
