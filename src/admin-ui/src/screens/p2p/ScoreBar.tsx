import { Box, Stack, Tooltip, Typography, useTheme, type Theme } from "@mui/material";
import type { PeerScore } from "@/screens/p2p/p2p.store";

/**
 * S8 — ScoreBar + per-component MiniBars.
 *
 * The composite score is rendered as a coloured gradient bar; the
 * three sub-components (accept / recency / diversity) appear as
 * narrow MiniBars under it so the operator can see the breakdown
 * at a glance.
 *
 * S8-audit M10 deferred: the gradient palette must pass WCAG AA
 * contrast in both light + dark themes. The colours below are
 * drawn from `palette.score.{low,mid,high}` declared in
 * `app/theme-augmentation.ts`; the contrast report lives at
 * `docs/admin-ui/design-bundle/score-contrast.md`.
 */
export function ScoreBar({ score }: { score: PeerScore }) {
  const theme = useTheme();
  const composite = Math.max(0, Math.min(100, score.composite));
  const tone = colourFor(composite, theme);

  return (
    <Stack
      spacing={0.5}
      data-testid="score-bar"
      data-composite={composite}
      sx={{ width: "100%" }}
    >
      <Tooltip title={`Composite ${composite} / 100`}>
        <Box
          sx={{
            height: 8,
            borderRadius: 4,
            bgcolor: theme.palette.action.hover,
            position: "relative",
            overflow: "hidden",
          }}
        >
          <Box
            sx={{
              position: "absolute",
              top: 0,
              left: 0,
              bottom: 0,
              width: `${composite}%`,
              bgcolor: tone,
            }}
          />
        </Box>
      </Tooltip>
      <Stack direction="row" spacing={0.5} aria-label="score-components">
        <MiniBar label="accept" value={score.accept} colour={theme.palette.severity.success.main} />
        <MiniBar label="recency" value={score.recency} colour={theme.palette.primary.main} />
        <MiniBar label="diversity" value={score.diversity} colour={theme.palette.severity.info.main} />
      </Stack>
      <Typography variant="caption" color="text.secondary">
        composite {composite}
      </Typography>
    </Stack>
  );
}

function MiniBar({
  label,
  value,
  colour,
}: {
  label: string;
  value: number;
  colour: string;
}) {
  const pct = Math.round(Math.max(0, Math.min(1, value)) * 100);
  return (
    <Tooltip title={`${label} ${pct}%`}>
      <Box
        data-testid={`minibar-${label}`}
        sx={{
          flex: 1,
          height: 3,
          borderRadius: 2,
          bgcolor: "action.hover",
          position: "relative",
          overflow: "hidden",
        }}
      >
        <Box
          sx={{
            position: "absolute",
            inset: 0,
            width: `${pct}%`,
            bgcolor: colour,
          }}
        />
      </Box>
    </Tooltip>
  );
}

function colourFor(composite: number, theme: Theme): string {
  if (composite >= 70) return theme.palette.severity.success.main;
  if (composite >= 40) return theme.palette.severity.warning.main;
  return theme.palette.severity.error.main;
}
