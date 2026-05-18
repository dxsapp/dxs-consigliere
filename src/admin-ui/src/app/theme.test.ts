import { describe, expect, it } from "vitest";
import { buildTheme } from "./theme";

describe("buildTheme — design-bundle token parity", () => {
  it("emits the dark palette when mode='dark'", () => {
    const theme = buildTheme("dark", "comfortable");
    expect(theme.palette.mode).toBe("dark");
    expect(theme.palette.primary.main).toBe("#8C9EFF");
    expect(theme.palette.background.default).toBe("#0E1116");
    expect(theme.palette.background.paper).toBe("#161B22");
  });

  it("emits the light palette when mode='light'", () => {
    const theme = buildTheme("light", "comfortable");
    expect(theme.palette.mode).toBe("light");
    expect(theme.palette.primary.main).toBe("#3D5AFE");
    expect(theme.palette.background.default).toBe("#F4F6F8");
  });

  it("carries the custom severity palette in both modes", () => {
    const dark = buildTheme("dark", "comfortable");
    expect(dark.palette.severity.warning.main).toBe("#FFA726");
    const light = buildTheme("light", "comfortable");
    expect(light.palette.severity.warning.main).toBe("#ED6C02");
  });

  it("carries the 5-stop score gradient in both modes", () => {
    const dark = buildTheme("dark", "comfortable");
    expect(dark.palette.score.s0).toBe("#EF5350");
    expect(dark.palette.score.s100).toBe("#66BB6A");
    const light = buildTheme("light", "comfortable");
    expect(light.palette.score.s0).toBe("#D32F2F");
  });

  it("pins the design-bundle deviations: borderRadius=8 + JetBrains Mono code variant", () => {
    const t = buildTheme("dark", "comfortable");
    expect(t.shape.borderRadius).toBe(8);
    expect(t.typography.code.fontFamily).toMatch(/JetBrains Mono/);
  });

  it("flips MuiDataGrid density between comfortable + dense", () => {
    const comfy = buildTheme("dark", "comfortable");
    const dense = buildTheme("dark", "dense");
    expect(comfy.components?.MuiDataGrid?.defaultProps?.density).toBe("standard");
    expect(dense.components?.MuiDataGrid?.defaultProps?.density).toBe("compact");
  });

  it("exposes the custom rowHeight + layout tokens", () => {
    const t = buildTheme("dark", "comfortable");
    expect(t.rowHeight).toEqual({ comfortable: 52, dense: 36 });
    expect(t.layout.drawerWidth).toBe(240);
    expect(t.layout.appBarHeight).toEqual({ desktop: 64, mobile: 56 });
  });
});
