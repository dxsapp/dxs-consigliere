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
import { TokensStore } from "@/screens/tokens/tokens.store";
import type { IAdminClient } from "@/lib/admin/admin-client";

/**
 * Track-a-new — Tokens. Lists currently-tracked tokens and lets an
 * operator register a new one by tokenId. Render shell only; lifecycle
 * owned by `TokensStore`.
 *
 * History mode: the add form tracks `forward_only` (see the store).
 */
export const TokensPage = observer(function TokensPage({
  admin,
}: {
  admin: IAdminClient;
}) {
  const store = useMemo(() => new TokensStore({ admin }), [admin]);
  useEffect(() => {
    void store.start();
    return () => store.dispose();
  }, [store]);

  return (
    <Stack spacing={3}>
      <Stack direction="row" alignItems="baseline" spacing={2} flexWrap="wrap" useFlexGap>
        <Typography variant="h4">Tokens</Typography>
        <Chip label={`${store.tokens.length} tracked`} size="small" variant="outlined" />
      </Stack>

      <Card>
        <CardHeader
          title="Track a new token"
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
                label="Token ID"
                value={store.formTokenId}
                onChange={(e) => store.setFormTokenId(e.target.value)}
                size="small"
                fullWidth
                required
                inputProps={{ "data-testid": "token-input" }}
              />
              <TextField
                label="Symbol (optional)"
                value={store.formSymbol}
                onChange={(e) => store.setFormSymbol(e.target.value)}
                size="small"
                fullWidth
                inputProps={{ "data-testid": "token-symbol-input" }}
              />
              <Button
                type="submit"
                variant="contained"
                disabled={!store.canSubmit}
                sx={{ minWidth: 120 }}
                data-testid="token-submit"
              >
                {store.submitting ? "Adding…" : "Track"}
              </Button>
            </Stack>
          </Box>
          {store.submitError && (
            <Alert severity="error" sx={{ mt: 2 }} data-testid="token-submit-error">
              Could not track token: {store.submitError}
            </Alert>
          )}
        </CardContent>
      </Card>

      {store.status === "loading" && !store.tokens.length && <LinearProgress />}
      {store.error && (
        <Alert severity="error">Failed to load tracked tokens: {store.error}</Alert>
      )}

      <Card>
        <CardHeader title="Tracked tokens" />
        <CardContent>
          {store.tokens.length === 0 && store.status === "ready" ? (
            <Typography variant="body2" color="text.secondary">
              No tracked tokens yet.
            </Typography>
          ) : (
            <Table size="small" data-testid="tokens-table">
              <TableHead>
                <TableRow>
                  <TableCell>Token ID</TableCell>
                  <TableCell>Symbol</TableCell>
                  <TableCell>Status</TableCell>
                  <TableCell>Readable</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {store.tokens.map((t) => (
                  <TableRow key={t.tokenId} hover>
                    <TableCell>
                      <Link
                        component={RouterLink}
                        to={`/tokens/${encodeURIComponent(t.tokenId)}`}
                        sx={{ fontFamily: "monospace", wordBreak: "break-all" }}
                      >
                        {t.tokenId}
                      </Link>
                    </TableCell>
                    <TableCell>{t.symbol || "—"}</TableCell>
                    <TableCell>
                      <Chip
                        size="small"
                        label={t.readiness.lifecycleStatus}
                        color={t.readiness.authoritative ? "success" : "default"}
                        variant="outlined"
                      />
                    </TableCell>
                    <TableCell>{t.readiness.readable ? "yes" : "no"}</TableCell>
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
