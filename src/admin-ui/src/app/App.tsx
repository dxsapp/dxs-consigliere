import { CssBaseline, ThemeProvider, createTheme } from "@mui/material";
import { BrowserRouter, Navigate, Route, Routes } from "react-router-dom";
import { LoginPage } from "@/screens/login/LoginPage";

// S0 placeholder theme — replaced in S1 with the design-bundle tokens.
const placeholderTheme = createTheme({
  palette: { mode: "dark" },
});

// S0 placeholder authenticated landing — S2 swaps this for the
// AppBar + Drawer shell and real routes.
function AuthedPlaceholder() {
  return (
    <div style={{ padding: 32, color: "white" }}>
      <h1>Consigliere Admin</h1>
      <p>S0 scaffold. Shell + routes ship in S2.</p>
    </div>
  );
}

export function App() {
  return (
    <ThemeProvider theme={placeholderTheme}>
      <CssBaseline />
      <BrowserRouter>
        <Routes>
          <Route path="/login" element={<LoginPage />} />
          {/* S0: every authed route is a placeholder. S2 wires the
              real shell + per-screen routes. The 401 redirect from
              the API client (lib/api/client.ts) sends the user back
              to /login if their cookie session expired. */}
          <Route path="/" element={<AuthedPlaceholder />} />
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </BrowserRouter>
    </ThemeProvider>
  );
}
