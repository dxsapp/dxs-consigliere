import { useMemo, useState } from "react";
import {
  Alert,
  Box,
  Card,
  CardContent,
  Chip,
  Divider,
  IconButton,
  Stack,
  Typography,
} from "@mui/material";
import ExpandMoreOutlinedIcon from "@mui/icons-material/ExpandMoreOutlined";
import ExpandLessOutlinedIcon from "@mui/icons-material/ExpandLessOutlined";

interface Props {
  title: string;
  data: unknown;
  defaultExpanded?: boolean;
}

type PlainObject = Record<string, unknown>;

function isPlainObject(value: unknown): value is PlainObject {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function formatLabel(key: string): string {
  return key
    .replace(/([a-z0-9])([A-Z])/g, "$1 $2")
    .replace(/[_-]+/g, " ")
    .replace(/\s+/g, " ")
    .trim()
    .replace(/^./, (char) => char.toUpperCase());
}

function formatScalar(value: string | number | boolean | null | undefined): React.ReactNode {
  if (value == null || value === "") {
    return (
      <Typography variant="body2" sx={{ color: "text.disabled" }}>
        unavailable
      </Typography>
    );
  }

  if (typeof value === "boolean") {
    return (
      <Chip
        size="small"
        label={value ? "Yes" : "No"}
        color={value ? "success" : "default"}
        variant={value ? "filled" : "outlined"}
        sx={{ height: 22 }}
      />
    );
  }

  return (
    <Typography
      variant="body2"
      sx={{
        fontFamily:
          typeof value === "string" && (value.length > 24 || /[:/_-]/.test(value))
            ? "JetBrains Mono, monospace"
            : undefined,
        fontSize: typeof value === "string" && value.length > 24 ? "0.8rem" : undefined,
        wordBreak: "break-word",
      }}
    >
      {String(value)}
    </Typography>
  );
}

function DataRow({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <Box
      sx={{
        display: "grid",
        gridTemplateColumns: { xs: "1fr", sm: "160px minmax(0, 1fr)" },
        gap: 1.5,
        py: 0.75,
        alignItems: "start",
      }}
    >
      <Typography
        variant="body2"
        sx={{ color: "text.disabled", fontSize: "0.78rem", textTransform: "none" }}
      >
        {label}
      </Typography>
      <Box sx={{ minWidth: 0 }}>{value}</Box>
    </Box>
  );
}

function DataObjectView({ value, depth = 0 }: { value: PlainObject; depth?: number }) {
  const entries = Object.entries(value);

  if (entries.length === 0) {
    return (
      <Typography variant="body2" sx={{ color: "text.disabled" }}>
        no fields
      </Typography>
    );
  }

  return (
    <Stack spacing={depth === 0 ? 0.75 : 1}>
      {entries.map(([key, child]) => (
        <DataRow
          key={key}
          label={formatLabel(key)}
          value={<DataValueView value={child} depth={depth + 1} />}
        />
      ))}
    </Stack>
  );
}

function DataArrayView({ value, depth }: { value: unknown[]; depth: number }) {
  if (value.length === 0) {
    return (
      <Typography variant="body2" sx={{ color: "text.disabled" }}>
        none
      </Typography>
    );
  }

  if (value.every((item) => !isPlainObject(item) && !Array.isArray(item))) {
    return (
      <Box sx={{ display: "flex", flexWrap: "wrap", gap: 0.75 }}>
        {value.map((item, index) => (
          <Chip
            key={`${String(item)}:${index}`}
            size="small"
            label={item == null || item === "" ? "unavailable" : String(item)}
            variant="outlined"
            sx={{ maxWidth: "100%" }}
          />
        ))}
      </Box>
    );
  }

  return (
    <Stack spacing={1}>
      {value.map((item, index) => (
        <Box
          key={index}
          sx={{
            border: "1px solid",
            borderColor: "divider",
            borderRadius: 2,
            p: 1.25,
            bgcolor: depth > 0 ? "background.default" : "transparent",
          }}
        >
          <Typography variant="caption" sx={{ color: "text.disabled", display: "block", mb: 0.75 }}>
            Item {index + 1}
          </Typography>
          <DataValueView value={item} depth={depth + 1} />
        </Box>
      ))}
    </Stack>
  );
}

function DataValueView({ value, depth = 0 }: { value: unknown; depth?: number }): React.ReactNode {
  if (isPlainObject(value)) {
    return (
      <Box
        sx={{
          borderLeft: depth > 0 ? "1px solid" : "none",
          borderColor: "divider",
          pl: depth > 0 ? 1.5 : 0,
        }}
      >
        <DataObjectView value={value} depth={depth} />
      </Box>
    );
  }

  if (Array.isArray(value)) {
    return <DataArrayView value={value} depth={depth} />;
  }

  if (typeof value === "string" || typeof value === "number" || typeof value === "boolean" || value == null) {
    return formatScalar(value);
  }

  return (
    <Typography variant="body2" sx={{ color: "text.secondary" }}>
      {String(value)}
    </Typography>
  );
}

export function JsonPanel({ title, data, defaultExpanded = false }: Props) {
  const [expanded, setExpanded] = useState(defaultExpanded);
  const isEmpty = data === null || data === undefined;
  const summary = useMemo(() => {
    if (isEmpty) return "unavailable";
    if (Array.isArray(data)) return `${data.length} item${data.length === 1 ? "" : "s"}`;
    if (isPlainObject(data)) return `${Object.keys(data).length} fields`;
    return "1 value";
  }, [data, isEmpty]);

  return (
    <Card variant="outlined">
      <CardContent sx={{ py: 2 }}>
        <Box
          sx={{
            display: "flex",
            alignItems: "center",
            justifyContent: "space-between",
            cursor: isEmpty ? "default" : "pointer",
            gap: 1,
          }}
          onClick={() => !isEmpty && setExpanded((value) => !value)}
        >
          <Box sx={{ minWidth: 0 }}>
            <Typography
              variant="overline"
              sx={{
                color: isEmpty ? "text.disabled" : "text.secondary",
                fontSize: "0.68rem",
              }}
            >
              {title}
            </Typography>
            {!isEmpty && (
              <Typography variant="caption" sx={{ color: "text.disabled", display: "block" }}>
                {summary}
              </Typography>
            )}
          </Box>
          {isEmpty ? (
            <Typography variant="caption" sx={{ color: "text.disabled" }}>
              unavailable
            </Typography>
          ) : (
            <IconButton size="small" sx={{ color: "text.disabled", flexShrink: 0 }}>
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
            {isPlainObject(data) || Array.isArray(data) ? (
              <DataValueView value={data} />
            ) : (
              <Alert severity="info" sx={{ py: 0 }}>
                <DataValueView value={data} />
              </Alert>
            )}
          </>
        )}
      </CardContent>
    </Card>
  );
}
