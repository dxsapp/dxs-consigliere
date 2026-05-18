import { Stack, Typography, useTheme, type Theme } from "@mui/material";
import { PieChart } from "@mui/x-charts";

/**
 * S8 — /24 subnet diversity donut. Lazy-imported by P2pPage so the
 * @mui/x-charts bundle stays out of the shell.
 */
export function SubnetDonut({
  subnets,
}: {
  subnets: Array<{ subnet: string; count: number }>;
}) {
  const theme = useTheme();
  if (subnets.length === 0) {
    return (
      <Stack alignItems="center">
        <Typography variant="body2" color="text.secondary">
          No subnet data yet.
        </Typography>
      </Stack>
    );
  }
  const top = subnets.slice(0, 8);
  const rest = subnets.slice(8).reduce((sum, s) => sum + s.count, 0);
  const data = [
    ...top.map((s, i) => ({
      id: s.subnet,
      value: s.count,
      label: s.subnet,
      color: paletteAt(theme, i),
    })),
    ...(rest > 0 ? [{ id: "other", value: rest, label: "other", color: theme.palette.text.disabled }] : []),
  ];
  return (
    <Stack data-testid="subnet-donut" alignItems="center" spacing={1}>
      <PieChart
        series={[{ data, innerRadius: 56, outerRadius: 90, paddingAngle: 1 }]}
        height={220}
        slotProps={{ legend: { sx: { display: "none" } } }}
      />
      <Stack
        direction="row"
        spacing={1}
        useFlexGap
        flexWrap="wrap"
        justifyContent="center"
      >
        {data.map((d) => (
          <Stack
            key={d.id}
            direction="row"
            spacing={0.5}
            alignItems="center"
          >
            <span
              style={{
                display: "inline-block",
                width: 8,
                height: 8,
                borderRadius: 4,
                background: d.color,
              }}
            />
            <Typography variant="caption" color="text.secondary">
              {d.label} · {d.value}
            </Typography>
          </Stack>
        ))}
      </Stack>
    </Stack>
  );
}

function paletteAt(theme: Theme, i: number): string {
  const pal = [
    theme.palette.primary.main,
    theme.palette.severity.success.main,
    theme.palette.severity.info.main,
    theme.palette.severity.warning.main,
    theme.palette.severity.error.main,
    theme.palette.secondary.main,
    theme.palette.primary.dark,
    theme.palette.secondary.dark,
  ];
  return pal[i % pal.length];
}
