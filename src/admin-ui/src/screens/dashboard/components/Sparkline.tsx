import { Box, useTheme } from "@mui/material";

/**
 * Minimal SVG sparkline. We avoid `@mui/x-charts` here so the
 * Dashboard chunk stays small; the heavier chart primitives land
 * in S9 Source Metrics + S8 P2P Pool (lazy per A1 M3 + S3-audit M6).
 *
 * Empty / single-point series render a flat baseline.
 */
export function Sparkline({
  data,
  width = 120,
  height = 36,
  ariaLabel,
}: {
  data: number[];
  width?: number;
  height?: number;
  ariaLabel: string;
}) {
  const theme = useTheme();
  const stroke = theme.palette.primary.main;
  if (data.length === 0) {
    return (
      <Box
        component="svg"
        viewBox={`0 0 ${width} ${height}`}
        width={width}
        height={height}
        role="img"
        aria-label={ariaLabel}
      >
        <line
          x1="0"
          y1={height / 2}
          x2={width}
          y2={height / 2}
          stroke={theme.palette.divider}
          strokeWidth={1}
        />
      </Box>
    );
  }

  const max = Math.max(...data);
  const min = Math.min(...data);
  const range = max - min || 1;
  const xStep = data.length > 1 ? width / (data.length - 1) : 0;
  const points = data
    .map((v, i) => {
      const x = i * xStep;
      const y = height - ((v - min) / range) * height;
      return `${x.toFixed(2)},${y.toFixed(2)}`;
    })
    .join(" ");

  return (
    <Box
      component="svg"
      viewBox={`0 0 ${width} ${height}`}
      width={width}
      height={height}
      role="img"
      aria-label={ariaLabel}
    >
      <polyline
        points={points}
        fill="none"
        stroke={stroke}
        strokeWidth={1.5}
        strokeLinejoin="round"
        strokeLinecap="round"
      />
    </Box>
  );
}
