import { Box, Card, CardContent, Chip, Stack, Typography } from "@mui/material";

interface KeyValueCardRow {
  label: string;
  value: React.ReactNode;
}

interface KeyValueCardProps {
  title: string;
  description?: React.ReactNode;
  status?: {
    label: string;
    color?: "default" | "success" | "warning" | "error" | "info";
    variant?: "filled" | "outlined";
  };
  rows: KeyValueCardRow[];
}

export function KeyValueCard({ title, description, status, rows }: KeyValueCardProps) {
  return (
    <Card variant="outlined" sx={{ borderRadius: 3 }}>
      <CardContent sx={{ p: 3 }}>
        <Stack spacing={2}>
          <Box sx={{ display: "flex", justifyContent: "space-between", gap: 2, alignItems: "flex-start" }}>
            <Box>
              <Typography variant="h6" sx={{ fontWeight: 600, mb: 0.25 }}>
                {title}
              </Typography>
              {description && (
                <Typography variant="body2" sx={{ color: "text.secondary" }}>
                  {description}
                </Typography>
              )}
            </Box>
            {status && (
              <Chip
                size="small"
                label={status.label}
                color={status.color ?? "default"}
                variant={status.variant ?? (status.color && status.color !== "default" ? "filled" : "outlined")}
              />
            )}
          </Box>

          <Stack spacing={0.75}>
            {rows.map((row) => (
              <Box
                key={row.label}
                sx={{
                  display: "grid",
                  gridTemplateColumns: { xs: "1fr", sm: "160px minmax(0, 1fr)" },
                  gap: 1.5,
                  alignItems: "start",
                }}
              >
                <Typography variant="body2" sx={{ color: "text.disabled", fontSize: "0.78rem" }}>
                  {row.label}
                </Typography>
                <Box sx={{ minWidth: 0 }}>{row.value}</Box>
              </Box>
            ))}
          </Stack>
        </Stack>
      </CardContent>
    </Card>
  );
}
