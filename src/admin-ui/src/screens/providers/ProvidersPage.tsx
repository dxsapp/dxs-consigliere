import {
  Alert,
  Card,
  CardContent,
  CardHeader,
  Chip,
  LinearProgress,
  Link,
  Stack,
  Typography,
} from "@mui/material";
import { useEffect, useState } from "react";
import type { IAdminClient } from "@/lib/admin/admin-client";
import type { AdminProvidersResponse } from "@/types/admin";

/**
 * S10 — Providers screen. Capability matrix: one card per provider
 * with roles, supported capabilities, recommended-for, active-for,
 * and missing requirements. Read-only.
 */
export function ProvidersPage({ admin }: { admin: IAdminClient }) {
  const [data, setData] = useState<AdminProvidersResponse | null>(null);
  const [status, setStatus] = useState<"loading" | "ready" | "error">("loading");
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const ctl = new AbortController();
    admin
      .getProviders(ctl.signal)
      .then((res) => {
        setData(res);
        setStatus("ready");
      })
      .catch((err) => {
        if (ctl.signal.aborted) return;
        setError(err instanceof Error ? err.message : "load failed");
        setStatus("error");
      });
    return () => ctl.abort();
  }, [admin]);

  return (
    <Stack spacing={3}>
      <Stack direction="row" alignItems="baseline" spacing={2} flexWrap="wrap" useFlexGap>
        <Typography variant="h4">Providers</Typography>
        <Chip label="S10" size="small" variant="outlined" />
      </Stack>

      {status === "loading" && <LinearProgress />}
      {error && <Alert severity="error">{error}</Alert>}

      <Stack spacing={2}>
        {(data?.providers ?? []).map((p) => (
          <Card key={p.providerId} data-testid={`provider-${p.providerId}`}>
            <CardHeader
              title={p.displayName}
              subheader={p.description}
              action={
                <Chip
                  size="small"
                  color={p.status === "Ready" ? "success" : "warning"}
                  label={p.status}
                />
              }
            />
            <CardContent>
              <Stack spacing={1.5}>
                <ChipRow label="Roles" items={p.roles} />
                <ChipRow label="Capabilities" items={p.supportedCapabilities} />
                {p.recommendedFor.length > 0 && (
                  <ChipRow label="Recommended for" items={p.recommendedFor} tone="primary" />
                )}
                {p.activeFor.length > 0 && (
                  <ChipRow label="Active for" items={p.activeFor} tone="success" />
                )}
                {p.missingRequirements.length > 0 && (
                  <Alert severity="warning">
                    Missing: {p.missingRequirements.join(", ")}
                  </Alert>
                )}
                {p.helpLinks.length > 0 && (
                  <Stack direction="row" spacing={1} useFlexGap flexWrap="wrap">
                    {p.helpLinks.map((l) => (
                      <Link key={l.url} href={l.url} target="_blank" rel="noopener">
                        {l.label}
                      </Link>
                    ))}
                  </Stack>
                )}
              </Stack>
            </CardContent>
          </Card>
        ))}
      </Stack>
    </Stack>
  );
}

function ChipRow({
  label,
  items,
  tone,
}: {
  label: string;
  items: string[];
  tone?: "primary" | "success" | "default";
}) {
  if (items.length === 0) return null;
  return (
    <Stack direction="row" spacing={1} useFlexGap flexWrap="wrap" alignItems="baseline">
      <Typography variant="caption" color="text.secondary">{label}</Typography>
      {items.map((it) => (
        <Chip key={it} size="small" label={it} color={tone ?? "default"} variant="outlined" />
      ))}
    </Stack>
  );
}
