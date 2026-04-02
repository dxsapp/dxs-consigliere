import { Card, CardActionArea, CardContent, Typography } from "@mui/material";

type Accent = "default" | "success" | "warning" | "error" | "info";

const ACCENT_COLORS: Record<Accent, string> = {
  default: "divider",
  success: "success.main",
  warning: "warning.main",
  error: "error.main",
  info: "info.main",
};

interface SummaryMetricCardProps {
  label: string;
  value: React.ReactNode;
  helper?: React.ReactNode;
  accent?: Accent;
  onClick?: () => void;
}

export function SummaryMetricCard({
  label,
  value,
  helper,
  accent = "default",
  onClick,
}: SummaryMetricCardProps) {
  const inner = (
    <CardContent sx={{ p: 2, "&:last-child": { pb: 2 } }}>
      <Typography
        variant="caption"
        sx={{
          color: "text.disabled",
          fontSize: "0.7rem",
          textTransform: "uppercase",
          letterSpacing: "0.06em",
        }}
      >
        {label}
      </Typography>
      <Typography
        variant="h4"
        sx={{
          fontWeight: 700,
          letterSpacing: "-0.03em",
          mt: 1,
          lineHeight: 1,
        }}
      >
        {value}
      </Typography>
      {helper && (
        <Typography variant="caption" sx={{ color: "text.secondary", mt: 0.75, display: "block" }}>
          {helper}
        </Typography>
      )}
    </CardContent>
  );

  return (
    <Card
      variant="outlined"
      sx={{
        borderRadius: 3,
        borderColor: accent === "default" ? "divider" : ACCENT_COLORS[accent],
      }}
    >
      {onClick ? (
        <CardActionArea onClick={onClick} sx={{ height: "100%" }}>
          {inner}
        </CardActionArea>
      ) : (
        inner
      )}
    </Card>
  );
}
