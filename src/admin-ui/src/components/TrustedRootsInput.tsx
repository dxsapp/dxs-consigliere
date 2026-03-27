import { TextField, Typography, Box } from "@mui/material";

const HEX64_RE = /^[0-9a-fA-F]{64}$/;

export interface ParsedRoots {
  roots: string[];
  invalid: string[];
}

export function parseTrustedRoots(raw: string): ParsedRoots {
  const parts = raw
    .split(/[\s,]+/)
    .map((value) => value.trim().toLowerCase())
    .filter(Boolean);

  const seen = new Set<string>();
  const roots: string[] = [];
  const invalid: string[] = [];

  for (const part of parts) {
    if (!HEX64_RE.test(part)) {
      invalid.push(part);
      continue;
    }

    if (seen.has(part))
      continue;

    seen.add(part);
    roots.push(part);
  }

  return { roots, invalid };
}

interface Props {
  value: string;
  onChange: (value: string) => void;
  label?: string;
  required?: boolean;
}

export function TrustedRootsInput({
  value,
  onChange,
  label = "Trusted Root TxIDs",
  required = false,
}: Props) {
  const { roots, invalid } = parseTrustedRoots(value);
  const hasError = invalid.length > 0;

  let helperText: React.ReactNode = "Paste one txid per line. Commas and spaces are also accepted.";
  if (hasError) {
    helperText = (
      <Box component="span" sx={{ color: "error.main" }}>
        {invalid.length} invalid {invalid.length === 1 ? "entry" : "entries"}: {" "}
        <Box component="span" sx={{ fontFamily: "monospace" }}>
          {invalid.slice(0, 2).join(", ")}
          {invalid.length > 2 ? ` +${invalid.length - 2} more` : ""}
        </Box>
      </Box>
    );
  } else if (roots.length > 0) {
    helperText = (
      <Typography component="span" variant="caption" sx={{ color: "success.main" }}>
        {roots.length} valid root{roots.length !== 1 ? "s" : ""}
      </Typography>
    );
  }

  return (
    <TextField
      label={label}
      value={value}
      onChange={(e) => onChange(e.target.value)}
      fullWidth
      multiline
      rows={4}
      size="small"
      required={required}
      placeholder={"One 64-char hex txid per line\ne.g. a1b2c3d4e5f6..."}
      error={hasError}
      helperText={helperText}
      inputProps={{ style: { fontFamily: "monospace", fontSize: "0.78rem" } }}
    />
  );
}
