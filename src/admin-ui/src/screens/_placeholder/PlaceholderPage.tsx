import { Box, Card, CardContent, Chip, Stack, Typography } from "@mui/material";
import { useParams } from "react-router-dom";

/**
 * S2 placeholder for every authed screen. Each screen slice (S4-
 * S10) replaces its corresponding placeholder with the real
 * implementation. Lives here so the router is end-to-end navigable
 * before screen work starts.
 */
export function PlaceholderPage({
  id,
  title,
  ownerSlice,
  description,
}: {
  id: string;
  title: string;
  ownerSlice: string;
  description: string;
}) {
  const params = useParams();
  const paramsList = Object.entries(params).filter(([, v]) => v !== undefined);
  return (
    <Stack spacing={2}>
      <Stack direction="row" alignItems="baseline" spacing={2}>
        <Typography variant="h4">{title}</Typography>
        <Chip label={ownerSlice} size="small" variant="outlined" />
      </Stack>
      <Typography variant="body2" color="text.secondary">
        {description}
      </Typography>
      <Card>
        <CardContent>
          <Typography variant="overline" color="text.secondary">
            placeholder · screen id
          </Typography>
          <Typography variant="code" component="div">
            {id}
          </Typography>
          {paramsList.length > 0 && (
            <Box sx={{ mt: 2 }}>
              <Typography variant="overline" color="text.secondary">
                route params
              </Typography>
              <Stack direction="row" spacing={1} useFlexGap flexWrap="wrap">
                {paramsList.map(([k, v]) => (
                  <Chip key={k} label={`${k}=${v}`} size="small" />
                ))}
              </Stack>
            </Box>
          )}
        </CardContent>
      </Card>
    </Stack>
  );
}
