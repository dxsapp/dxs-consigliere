/**
 * MUI theme augmentation — custom palette + typography + theme
 * fields that live alongside the Material defaults. Imported once
 * for side-effects from `theme.ts`; never imported by feature code.
 *
 * Token source: docs/admin-ui/design-bundle/project/theme.jsx.
 */
import type * as React from "react";

// Bring MuiDataGrid + MuiDataGridPro defaultProps into the theme
// shape (Core Rule §1 — MuiDataGrid is the only table primitive).
import "@mui/x-data-grid/themeAugmentation";

declare module "@mui/material/styles" {
  interface SeverityPaletteColor {
    main: string;
    bg: string;
    contrastText: string;
  }
  interface SeverityPalette {
    info: SeverityPaletteColor;
    success: SeverityPaletteColor;
    warning: SeverityPaletteColor;
    error: SeverityPaletteColor;
  }

  /** 5-stop gradient for the 0-100 peer score on the P2P Pool screen. */
  interface ScorePalette {
    s0: string;
    s25: string;
    s50: string;
    s75: string;
    s100: string;
  }

  interface Palette {
    severity: SeverityPalette;
    score: ScorePalette;
  }
  interface PaletteOptions {
    severity?: SeverityPalette;
    score?: ScorePalette;
  }

  interface TypeBackground {
    elev1: string;
    elev2: string;
  }

  interface TypographyVariants {
    /** Monospace variant for hex / txid / config-key spans. */
    code: React.CSSProperties;
  }
  interface TypographyVariantsOptions {
    code?: React.CSSProperties;
  }

  interface Theme {
    rowHeight: { comfortable: number; dense: number };
    layout: {
      appBarHeight: { desktop: number; mobile: number };
      drawerWidth: number;
    };
  }
  interface ThemeOptions {
    rowHeight?: { comfortable?: number; dense?: number };
    layout?: {
      appBarHeight?: { desktop?: number; mobile?: number };
      drawerWidth?: number;
    };
  }
}

declare module "@mui/material/Typography" {
  interface TypographyPropsVariantOverrides {
    code: true;
  }
}
