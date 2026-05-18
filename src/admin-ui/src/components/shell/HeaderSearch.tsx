import { Autocomplete, Box, InputAdornment, TextField, Typography, Stack } from "@mui/material";
import SearchIcon from "@mui/icons-material/Search";
import { useState, type KeyboardEvent } from "react";
import { useNavigate } from "react-router-dom";
import { entityToPath, parseSearchQuery, type SearchEntity } from "@/lib/search/grammar";

/**
 * Smart-search input for the AppBar. Per Core Rule §14 (A1 M6):
 * single freeSolo Autocomplete; auto-recognises format on Enter
 * via `parseSearchQuery`.
 *
 * - empty           → no-op
 * - resolved entity → router-navigates via `entityToPath`
 * - ambiguous       → renders the "did you mean ..." chip list as
 *                     dropdown options; operator clicks to choose
 */
export function HeaderSearch() {
  const navigate = useNavigate();
  const [raw, setRaw] = useState("");
  const [ambiguity, setAmbiguity] = useState<SearchEntity | null>(null);

  const onEnter = (ev: KeyboardEvent<HTMLDivElement>) => {
    if (ev.key !== "Enter") return;
    const result = parseSearchQuery(raw);
    if (result.kind === "empty") return;
    if (result.kind === "ambiguous") {
      setAmbiguity(result);
      return;
    }
    setAmbiguity(null);
    const path = entityToPath(result);
    if (path) navigate(path);
  };

  const options =
    ambiguity?.kind === "ambiguous"
      ? ambiguity.candidates.map((c) => labelFor(c))
      : [];

  const onPickAmbiguous = (label: string) => {
    if (ambiguity?.kind !== "ambiguous") return;
    const pick = ambiguity.candidates.find((c) => labelFor(c) === label);
    if (!pick) return;
    setAmbiguity(null);
    const path = entityToPath(pick);
    if (path) navigate(path);
  };

  return (
    <Box sx={{ flex: 1, maxWidth: 560 }}>
      <Autocomplete
        freeSolo
        size="small"
        inputValue={raw}
        onInputChange={(_e, v) => {
          setRaw(v);
          if (ambiguity) setAmbiguity(null);
        }}
        options={options}
        open={ambiguity?.kind === "ambiguous"}
        // open={true} is gated by ambiguity above; close when the
        // user picks an option.
        onChange={(_e, v) => {
          if (typeof v === "string") onPickAmbiguous(v);
        }}
        // Render the "did you mean" chip-style row inside the dropdown.
        renderOption={(props, option) => (
          <li {...props} key={option}>
            <Stack direction="row" spacing={1} alignItems="center">
              <Typography variant="caption" color="text.secondary">
                did you mean
              </Typography>
              <Typography variant="body2">{option}</Typography>
            </Stack>
          </li>
        )}
        renderInput={(params) => (
          <TextField
            {...params}
            placeholder="Search tx / address / token / block…"
            onKeyDown={onEnter}
            InputProps={{
              ...params.InputProps,
              startAdornment: (
                <InputAdornment position="start">
                  <SearchIcon fontSize="small" />
                </InputAdornment>
              ),
            }}
          />
        )}
      />
    </Box>
  );
}

function labelFor(e: SearchEntity): string {
  switch (e.kind) {
    case "tx":
      return `transaction ${e.txid.slice(0, 8)}…`;
    case "block-hash":
      return `block hash ${e.hash.slice(0, 8)}…`;
    case "block-height":
      return `block #${e.height}`;
    case "address":
      return `address ${e.address.slice(0, 8)}…`;
    case "token":
      return `token ${e.tokenId}`;
    default:
      return "";
  }
}
