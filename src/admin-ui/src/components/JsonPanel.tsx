import { useState } from "react";
import { Box, Card, CardContent, Divider, IconButton, Typography } from "@mui/material";
import ExpandMoreOutlinedIcon from "@mui/icons-material/ExpandMoreOutlined";
import ExpandLessOutlinedIcon from "@mui/icons-material/ExpandLessOutlined";

interface Props {
  title: string;
  data: unknown;
  defaultExpanded?: boolean;
}

export function JsonPanel({ title, data, defaultExpanded = false }: Props) {
  const [expanded, setExpanded] = useState(defaultExpanded);
  const isEmpty = data === null || data === undefined;

  return (
    <Card variant="outlined">
      <CardContent sx={{ py: 2 }}>
        <Box
          sx={{
            display: "flex",
            alignItems: "center",
            justifyContent: "space-between",
            cursor: isEmpty ? "default" : "pointer",
          }}
          onClick={() => !isEmpty && setExpanded((v) => !v)}
        >
          <Typography
            variant="overline"
            sx={{
              color: isEmpty ? "text.disabled" : "text.secondary",
              fontSize: "0.68rem",
            }}
          >
            {title}
          </Typography>
          {isEmpty ? (
            <Typography variant="caption" sx={{ color: "text.disabled" }}>
              unavailable
            </Typography>
          ) : (
            <IconButton size="small" sx={{ color: "text.disabled" }}>
              {expanded ? (
                <ExpandLessOutlinedIcon fontSize="small" />
              ) : (
                <ExpandMoreOutlinedIcon fontSize="small" />
              )}
            </IconButton>
          )}
        </Box>
        {!isEmpty && expanded && (
          <>
            <Divider sx={{ my: 1 }} />
            <Box
              component="pre"
              sx={{
                fontSize: "0.75rem",
                fontFamily: "monospace",
                color: "text.secondary",
                bgcolor: "background.default",
                borderRadius: 1,
                p: 1.5,
                overflow: "auto",
                maxHeight: 400,
                m: 0,
              }}
            >
              {JSON.stringify(data, null, 2)}
            </Box>
          </>
        )}
      </CardContent>
    </Card>
  );
}
