import { createTheme } from "@mui/material/styles";

// ─── Design Contract: Modern Dark – Deep Slate ────────────────────────────────
//
// Background layers  (darkest → elevated)
//   app bg      #080810
//   surface     #0f0f1a   cards, panels
//   elevated    #161628   drawers, popovers
//   overlay     #1e1e35   modals, dialogs
//
// Borders
//   subtle      rgba(255,255,255, 0.05)   dividers
//   card        rgba(255,255,255, 0.08)   card outlines
//   active      rgba(255,255,255, 0.14)   focused / hover borders
//
// Accent – Cold Indigo
//   main        #6366f1   (indigo-500)
//   light       #818cf8   (indigo-400)
//   dark        #4f46e5   (indigo-600)
//
// Status
//   success     #22c55e   live / full-history / healthy
//   warning     #f59e0b   degraded / partial / unknown-root
//   error       #ef4444   failure / blocked
//   info        #38bdf8   backfilling / pending
//
// Text
//   primary     #f1f5f9
//   secondary   #94a3b8
//   disabled    #475569
//
// Monospace  – JetBrains Mono → Fira Code → system-ui monospace
//   used for addresses, txids, token ids, raw state strings
// ─────────────────────────────────────────────────────────────────────────────

export const theme = createTheme({
  palette: {
    mode: "dark",
    background: {
      default: "#080810",
      paper: "#0f0f1a",
    },
    primary: {
      main: "#6366f1",
      light: "#818cf8",
      dark: "#4f46e5",
      contrastText: "#ffffff",
    },
    secondary: {
      main: "#38bdf8",
      contrastText: "#ffffff",
    },
    success: {
      main: "#22c55e",
      contrastText: "#ffffff",
    },
    warning: {
      main: "#f59e0b",
      contrastText: "#000000",
    },
    error: {
      main: "#ef4444",
      contrastText: "#ffffff",
    },
    info: {
      main: "#38bdf8",
      contrastText: "#000000",
    },
    text: {
      primary: "#f1f5f9",
      secondary: "#94a3b8",
      disabled: "#475569",
    },
    divider: "rgba(255, 255, 255, 0.08)",
  },

  typography: {
    fontFamily: '"Inter", "system-ui", sans-serif',
    fontSize: 13,
    h1: { fontSize: "1.75rem", fontWeight: 600, letterSpacing: "-0.02em" },
    h2: { fontSize: "1.375rem", fontWeight: 600, letterSpacing: "-0.015em" },
    h3: { fontSize: "1.125rem", fontWeight: 600, letterSpacing: "-0.01em" },
    h4: { fontSize: "0.9375rem", fontWeight: 600 },
    h5: { fontSize: "0.875rem", fontWeight: 600 },
    h6: { fontSize: "0.8125rem", fontWeight: 600 },
    body1: { fontSize: "0.875rem", lineHeight: 1.6 },
    body2: { fontSize: "0.8125rem", lineHeight: 1.5, color: "#94a3b8" },
    caption: { fontSize: "0.75rem", color: "#94a3b8" },
    overline: { fontSize: "0.6875rem", letterSpacing: "0.08em", fontWeight: 600 },
  },

  shape: {
    borderRadius: 8,
  },

  components: {
    MuiCssBaseline: {
      styleOverrides: {
        // monospace class for addresses / txids / token ids
        ".mono": {
          fontFamily:
            '"JetBrains Mono", "Fira Code", "Cascadia Code", ui-monospace, monospace',
          fontSize: "0.8125rem",
          letterSpacing: "0.01em",
        },
      },
    },

    MuiCard: {
      styleOverrides: {
        root: {
          backgroundImage: "none",
          backgroundColor: "#0f0f1a",
          border: "1px solid rgba(255, 255, 255, 0.08)",
        },
      },
    },

    MuiPaper: {
      styleOverrides: {
        root: {
          backgroundImage: "none",
          backgroundColor: "#0f0f1a",
        },
        elevation2: { backgroundColor: "#161628" },
        elevation3: { backgroundColor: "#161628" },
        elevation8: { backgroundColor: "#1e1e35" },
        elevation24: { backgroundColor: "#1e1e35" },
      },
    },

    MuiTableHead: {
      styleOverrides: {
        root: {
          "& .MuiTableCell-head": {
            backgroundColor: "#0a0a14",
            color: "#94a3b8",
            fontSize: "0.6875rem",
            fontWeight: 600,
            letterSpacing: "0.06em",
            textTransform: "uppercase",
            borderBottom: "1px solid rgba(255, 255, 255, 0.08)",
          },
        },
      },
    },

    MuiTableRow: {
      styleOverrides: {
        root: {
          "&:hover": {
            backgroundColor: "rgba(99, 102, 241, 0.04)",
          },
          "& .MuiTableCell-root": {
            borderBottom: "1px solid rgba(255, 255, 255, 0.05)",
          },
        },
      },
    },

    MuiChip: {
      styleOverrides: {
        root: {
          fontSize: "0.6875rem",
          fontWeight: 600,
          height: 22,
          borderRadius: 4,
        },
      },
    },

    MuiButton: {
      styleOverrides: {
        root: {
          textTransform: "none",
          fontWeight: 600,
          fontSize: "0.875rem",
        },
      },
    },

    MuiDrawer: {
      styleOverrides: {
        paper: {
          backgroundColor: "#161628",
          borderLeft: "1px solid rgba(255, 255, 255, 0.08)",
        },
      },
    },

    MuiDialog: {
      styleOverrides: {
        paper: {
          backgroundColor: "#1e1e35",
          border: "1px solid rgba(255, 255, 255, 0.10)",
        },
      },
    },

    MuiTooltip: {
      styleOverrides: {
        tooltip: {
          backgroundColor: "#1e1e35",
          border: "1px solid rgba(255, 255, 255, 0.10)",
          fontSize: "0.75rem",
        },
      },
    },

    MuiAlert: {
      styleOverrides: {
        root: {
          fontSize: "0.8125rem",
        },
      },
    },
  },
});
