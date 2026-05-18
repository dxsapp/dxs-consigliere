import {
  AppBar,
  Box,
  Button,
  Card,
  CardContent,
  CardHeader,
  Chip,
  Container,
  Divider,
  IconButton,
  Snackbar,
  Stack,
  Toolbar,
  Typography,
  useTheme,
} from "@mui/material";
import LightModeIcon from "@mui/icons-material/LightMode";
import DarkModeIcon from "@mui/icons-material/DarkMode";
import ViewComfyIcon from "@mui/icons-material/ViewComfy";
import ViewCompactIcon from "@mui/icons-material/ViewCompact";
import { observer } from "mobx-react-lite";
import { useState } from "react";
import type { PrefStore } from "@/stores/pref.store";

/**
 * S1 — visual regression baseline. Renders every theme primitive
 * (typography scale, severity colours, score gradient, MUI button +
 * card + chip + AppBar + snackbar) and exposes the theme-mode +
 * density toggles. Confirms that token edits show up everywhere.
 *
 * Not in the production sidebar; reachable only via the explicit
 * `/dev/theme-demo` URL.
 */
export const DevThemeDemoPage = observer(function DevThemeDemoPage({
  prefs,
}: {
  prefs: PrefStore;
}) {
  const theme = useTheme();
  const [snackOpen, setSnackOpen] = useState(false);

  return (
    <Box sx={{ minHeight: "100vh", background: theme.palette.background.default }}>
      <AppBar position="sticky">
        <Toolbar>
          <Typography variant="h6" sx={{ flexGrow: 1 }}>
            theme-demo
          </Typography>
          <IconButton onClick={prefs.toggleMode} aria-label="toggle theme mode">
            {prefs.mode === "dark" ? <LightModeIcon /> : <DarkModeIcon />}
          </IconButton>
          <IconButton onClick={prefs.toggleDensity} aria-label="toggle density">
            {prefs.density === "comfortable" ? <ViewCompactIcon /> : <ViewComfyIcon />}
          </IconButton>
        </Toolbar>
      </AppBar>

      <Container maxWidth="lg" sx={{ py: 4 }}>
        <Stack spacing={3}>
          <StatusLine prefs={prefs} />

          <Card>
            <CardHeader title="Typography" subheader="Roboto + JetBrains Mono (code variant)" />
            <CardContent>
              <Typography variant="h1">h1 — Consigliere</Typography>
              <Typography variant="h2">h2 — Admin</Typography>
              <Typography variant="h3">h3 — Dashboard</Typography>
              <Typography variant="h4">h4 — Section</Typography>
              <Typography variant="h5">h5 — Subsection</Typography>
              <Typography variant="h6">h6 — Group</Typography>
              <Typography variant="subtitle1">subtitle1 — Subtitle</Typography>
              <Typography variant="subtitle2">subtitle2 — Subtitle</Typography>
              <Typography variant="body1">
                body1 — Always-open dashboard on a second monitor; real-time push critical.
              </Typography>
              <Typography variant="body2">
                body2 — Cards move between columns in real time as state transitions arrive.
              </Typography>
              <Typography variant="caption" component="div">
                caption — last updated 14:23:11 UTC (2 min ago)
              </Typography>
              <Typography variant="overline" component="div">
                overline — system status
              </Typography>
              <Typography variant="code" component="div" sx={{ mt: 1 }}>
                code — aabbccdd11223344556677889900aabbccdd11223344556677889900aabbcc
              </Typography>
            </CardContent>
          </Card>

          <Card>
            <CardHeader title="Severity palette" />
            <CardContent>
              <Stack direction="row" spacing={1} useFlexGap flexWrap="wrap">
                {(["info", "success", "warning", "error"] as const).map((sev) => {
                  const col = theme.palette.severity[sev];
                  return (
                    <Chip
                      key={sev}
                      label={sev}
                      sx={{
                        bgcolor: col.bg,
                        color: col.main,
                        border: `1px solid ${col.main}`,
                      }}
                    />
                  );
                })}
              </Stack>
            </CardContent>
          </Card>

          <Card>
            <CardHeader title="Score gradient (0-100)" subheader="P2P Pool peer score colour scale" />
            <CardContent>
              <Stack direction="row" spacing={0} useFlexGap>
                {(["s0", "s25", "s50", "s75", "s100"] as const).map((k) => (
                  <Box
                    key={k}
                    sx={{
                      flex: 1,
                      height: 48,
                      background: theme.palette.score[k],
                      display: "flex",
                      alignItems: "center",
                      justifyContent: "center",
                      color: theme.palette.getContrastText(theme.palette.score[k]),
                      fontFamily: theme.typography.code.fontFamily,
                      fontSize: "0.8125rem",
                    }}
                  >
                    {k}
                  </Box>
                ))}
              </Stack>
            </CardContent>
          </Card>

          <Card>
            <CardHeader title="Buttons + chips" />
            <CardContent>
              <Stack direction="row" spacing={1} useFlexGap flexWrap="wrap">
                <Button variant="contained">primary</Button>
                <Button variant="outlined">outlined</Button>
                <Button variant="text">text</Button>
                <Button variant="contained" color="error">
                  destructive
                </Button>
                <Button variant="contained" disabled>
                  disabled
                </Button>
              </Stack>
              <Divider sx={{ my: 2 }} />
              <Stack direction="row" spacing={1} useFlexGap flexWrap="wrap">
                <Chip label="default" />
                <Chip label="primary" color="primary" />
                <Chip label="success" color="success" />
                <Chip label="warning" color="warning" />
                <Chip label="error" color="error" />
                <Chip label="outlined" variant="outlined" />
              </Stack>
            </CardContent>
          </Card>

          <Card>
            <CardHeader title="Density" subheader={`row height = ${theme.rowHeight[prefs.density]} px`} />
            <CardContent>
              <Stack divider={<Divider />}>
                {["row-a", "row-b", "row-c"].map((id) => (
                  <Box
                    key={id}
                    sx={{
                      display: "flex",
                      alignItems: "center",
                      height: theme.rowHeight[prefs.density],
                      px: 1,
                    }}
                  >
                    <Typography variant="body2">{id}</Typography>
                  </Box>
                ))}
              </Stack>
            </CardContent>
          </Card>

          <Card>
            <CardHeader title="Layout constants" />
            <CardContent>
              <Typography variant="code" component="div">
                appBar (desktop / mobile) = {theme.layout.appBarHeight.desktop} /{" "}
                {theme.layout.appBarHeight.mobile}
              </Typography>
              <Typography variant="code" component="div">
                drawerWidth = {theme.layout.drawerWidth}
              </Typography>
              <Typography variant="code" component="div">
                shape.borderRadius = {theme.shape.borderRadius}
              </Typography>
              <Typography variant="code" component="div">
                spacing(1) = {theme.spacing(1)}
              </Typography>
            </CardContent>
          </Card>

          <Button variant="contained" onClick={() => setSnackOpen(true)}>
            test snackbar
          </Button>
        </Stack>
      </Container>

      <Snackbar
        open={snackOpen}
        autoHideDuration={3000}
        onClose={() => setSnackOpen(false)}
        message="Snackbar uses theme.palette.text colours."
      />
    </Box>
  );
});

const StatusLine = observer(function StatusLine({ prefs }: { prefs: PrefStore }) {
  return (
    <Card>
      <CardContent>
        <Stack direction="row" spacing={2} alignItems="center">
          <Chip label={`mode: ${prefs.mode}`} color={prefs.mode === "dark" ? "primary" : "secondary"} />
          <Chip label={`density: ${prefs.density}`} variant="outlined" />
          <Typography variant="body2" color="text.secondary">
            Toggle from the app bar; both choices persist via mobx-persist-store.
          </Typography>
        </Stack>
      </CardContent>
    </Card>
  );
});
