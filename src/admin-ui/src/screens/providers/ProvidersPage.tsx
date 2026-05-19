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
import { observer } from "mobx-react-lite";
import { useEffect, useMemo } from "react";
import { ProvidersStore } from "@/screens/providers/providers.store";
import type { IAdminClient } from "@/lib/admin/admin-client";

/**
 * S10 — Providers screen. Capability matrix: one card per provider
 * with roles, supported capabilities, recommended-for, active-for,
 * and missing requirements. Read-only. Lifecycle owned by
 * `ProvidersStore` (S7-S12-audit M2).
 */
export const ProvidersPage = observer(function ProvidersPage({
  admin,
}: {
  admin: IAdminClient;
}) {
  const store = useMemo(() => new ProvidersStore({ admin }), [admin]);
  useEffect(() => {
    void store.start();
    return () => store.dispose();
  }, [store]);

  const data = store.data;

  return (
    <Stack spacing={3}>
      <Stack direction="row" alignItems="baseline" spacing={2} flexWrap="wrap" useFlexGap>
        <Typography variant="h4">Providers</Typography>
        <Chip label="S10" size="small" variant="outlined" />
      </Stack>

      {store.status === "loading" && <LinearProgress />}
      {store.error && <Alert severity="error">{store.error}</Alert>}

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
});

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
