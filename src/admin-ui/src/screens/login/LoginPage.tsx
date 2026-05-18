import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  CardHeader,
  Stack,
  TextField,
  Typography,
} from "@mui/material";
import { observer } from "mobx-react-lite";
import { useState } from "react";
import { Navigate, useLocation, useNavigate } from "react-router-dom";
import type { AuthStore } from "@/stores/root";
import { LANDING_PATH } from "@/app/routes";

/**
 * Login screen.
 *
 * S2 ships the form + the auth-status flip via the synthetic
 * `signInSynthetic` seam in AuthStore. The placeholder is
 * intentionally minimal (no password, no validation) — the visual
 * shape is real but the auth wire happens in S3 when the API
 * client gets `me / login / logout`.
 *
 * Redirect: after sign-in, navigate to the `from` location that
 * the AuthGuard captured, falling back to the landing path.
 */
export const LoginPage = observer(function LoginPage({
  auth,
}: {
  auth: AuthStore;
}) {
  const navigate = useNavigate();
  const location = useLocation();
  const [name, setName] = useState("");
  const fromState = (location.state as { from?: string } | null)?.from;

  // S2-audit L1: an already-authenticated visitor on /login
  // bounces to the saved `from` location (if any) or the landing
  // path — not a blank screen.
  if (auth.isAuthenticated) {
    return <Navigate to={fromState ?? LANDING_PATH} replace />;
  }

  const onSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    auth.signInSynthetic(name);
    navigate(fromState ?? LANDING_PATH, { replace: true });
  };

  return (
    <Box
      sx={{
        minHeight: "100vh",
        display: "grid",
        placeItems: "center",
        background: (theme) => theme.palette.background.default,
      }}
    >
      <Card sx={{ width: 380 }}>
        <CardHeader title="Consigliere Admin" subheader="Sign in" />
        <CardContent>
          <Alert severity="info" sx={{ mb: 2 }}>
            S2 placeholder. The real <code>/api/admin/auth/login</code> wire
            lands in S3 (cookie auth).
          </Alert>
          <form onSubmit={onSubmit}>
            <Stack spacing={2}>
              <TextField
                label="Operator name"
                value={name}
                onChange={(e) => setName(e.target.value)}
                autoFocus
                fullWidth
                size="small"
              />
              <Button type="submit" variant="contained" fullWidth>
                Sign in
              </Button>
              {auth.lastError && (
                <Typography variant="caption" color="error">
                  {auth.lastError}
                </Typography>
              )}
            </Stack>
          </form>
        </CardContent>
      </Card>
    </Box>
  );
});
