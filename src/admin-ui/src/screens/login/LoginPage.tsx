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
 * S3 ships the cookie-mode POST: `auth.signIn({ username, password })`
 * calls the AuthClient, which hits `POST /api/admin/auth/login`
 * (or the MockAuthClient seed in `VITE_API_MODE=mock`). On success
 * navigate to the captured `state.from` location, falling back to
 * the landing path.
 */
export const LoginPage = observer(function LoginPage({
  auth,
}: {
  auth: AuthStore;
}) {
  const navigate = useNavigate();
  const location = useLocation();
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const fromState = (location.state as { from?: string } | null)?.from;

  // S2-audit L1: an already-authenticated visitor on /login
  // bounces to the saved `from` location (if any) or the landing
  // path — not a blank screen.
  if (auth.isAuthenticated) {
    return <Navigate to={fromState ?? LANDING_PATH} replace />;
  }

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setSubmitting(true);
    const ok = await auth.signIn({ username, password });
    setSubmitting(false);
    if (ok) navigate(fromState ?? LANDING_PATH, { replace: true });
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
          {auth.setupRequired && (
            <Alert severity="warning" sx={{ mb: 2 }}>
              Setup required — finish first-run configuration before signing in.
            </Alert>
          )}
          {!auth.enabled && (
            <Alert severity="info" sx={{ mb: 2 }}>
              Auth is disabled in this environment.
            </Alert>
          )}
          <form onSubmit={onSubmit}>
            <Stack spacing={2}>
              <TextField
                label="Operator name"
                value={username}
                onChange={(e) => setUsername(e.target.value)}
                autoFocus
                fullWidth
                size="small"
                disabled={submitting}
              />
              <TextField
                label="Password"
                type="password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                fullWidth
                size="small"
                disabled={submitting}
              />
              <Button type="submit" variant="contained" fullWidth disabled={submitting}>
                {submitting ? "Signing in…" : "Sign in"}
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
