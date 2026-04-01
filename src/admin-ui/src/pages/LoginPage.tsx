import { useState } from "react";
import { Navigate, useNavigate, useLocation } from "react-router-dom";
import { observer } from "mobx-react-lite";
import {
  Box,
  Card,
  CardContent,
  TextField,
  Button,
  Typography,
  Alert,
  Stack,
} from "@mui/material";
import { authStore } from "@/stores/auth.store";

export const LoginPage = observer(function LoginPage() {
  if (authStore.setupRequired) {
    return <Navigate to="/setup" replace />;
  }

  if (authStore.isAuthenticated) {
    return <Navigate to="/" replace />;
  }

  const navigate = useNavigate();
  const location = useLocation();
  const from = (location.state as { from?: Location })?.from?.pathname ?? "/";

  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [submitting, setSubmitting] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!username || !password) return;

    setSubmitting(true);
    const ok = await authStore.login(username, password);
    setSubmitting(false);

    if (ok) navigate(from, { replace: true });
  };

  return (
    <Box
      sx={{
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        minHeight: "100vh",
        bgcolor: "background.default",
      }}
    >
      <Card sx={{ width: 360 }}>
        <CardContent sx={{ p: 4 }}>
          <Stack spacing={3}>
            <Stack spacing={0.5}>
              <Typography variant="h3" sx={{ color: "primary.light" }}>
                Consigliere
              </Typography>
              <Typography variant="body2">Admin shell</Typography>
            </Stack>

            {authStore.loginError && (
              <Alert severity="error" onClose={() => authStore.clearLoginError()}>
                {authStore.loginError}
              </Alert>
            )}

            <Box component="form" onSubmit={handleSubmit}>
              <Stack spacing={2}>
                <TextField
                  label="Username"
                  value={username}
                  onChange={(e) => setUsername(e.target.value)}
                  autoComplete="username"
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
                  autoComplete="current-password"
                  fullWidth
                  size="small"
                  disabled={submitting}
                />
                <Button
                  type="submit"
                  variant="contained"
                  fullWidth
                  disabled={submitting || !username || !password}
                >
                  {submitting ? "Signing in…" : "Sign in"}
                </Button>
              </Stack>
            </Box>
          </Stack>
        </CardContent>
      </Card>
    </Box>
  );
});
