import { Chip } from "@mui/material";

type ChipColor = "success" | "error" | "warning" | "info" | "default";

function resolveColor(readiness: string): ChipColor {
  const s = readiness.toLowerCase();
  if (/live|ready|ok|healthy/.test(s)) return "success";
  if (/degrad|fail|error|block/.test(s)) return "error";
  if (/backfill|pending|partial|unknown/.test(s)) return "warning";
  if (/sync|init|load/.test(s)) return "info";
  return "default";
}

interface Props {
  readiness: string;
  size?: "small" | "medium";
}

export function ReadinessChip({ readiness, size = "small" }: Props) {
  return (
    <Chip
      label={readiness}
      color={resolveColor(readiness)}
      size={size}
      variant="outlined"
      sx={{ fontFamily: "monospace", fontSize: "0.7rem", height: 22 }}
    />
  );
}
