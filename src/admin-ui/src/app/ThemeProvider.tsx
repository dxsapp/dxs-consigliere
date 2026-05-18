import { CssBaseline, ThemeProvider as MuiThemeProvider } from "@mui/material";
import { observer } from "mobx-react-lite";
import { useMemo, type ReactNode } from "react";
import { buildTheme } from "@/app/theme";
import type { PrefStore } from "@/stores/pref.store";

/**
 * Reactive ThemeProvider — re-derives the MUI theme whenever the
 * pref store's mode or density changes. Wraps `CssBaseline` so the
 * background/text defaults flip with the palette.
 */
export const ThemeProvider = observer(function ThemeProvider({
  prefs,
  children,
}: {
  prefs: PrefStore;
  children: ReactNode;
}) {
  const theme = useMemo(
    () => buildTheme(prefs.mode, prefs.density),
    [prefs.mode, prefs.density]
  );
  return (
    <MuiThemeProvider theme={theme}>
      <CssBaseline />
      {children}
    </MuiThemeProvider>
  );
});
