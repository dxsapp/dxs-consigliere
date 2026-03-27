import { useEffect } from "react";
import { Navigate, useLocation } from "react-router-dom";
import { observer } from "mobx-react-lite";
import { Box, CircularProgress } from "@mui/material";
import { authStore } from "@/stores/auth.store";

// Hydrates auth state once at route level; pages do not own this.
export const AuthGuard = observer(function AuthGuard({
  children,
}: {
  children: React.ReactNode;
}) {
  const location = useLocation();

  useEffect(() => {
    void authStore.initialize();
  }, []);

  if (authStore.isInitializing) {
    return (
      <Box
        sx={{
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          height: "100vh",
          bgcolor: "background.default",
        }}
      >
        <CircularProgress size={28} />
      </Box>
    );
  }

  if (!authStore.isAuthenticated) {
    return <Navigate to="/login" state={{ from: location }} replace />;
  }

  return <>{children}</>;
});
