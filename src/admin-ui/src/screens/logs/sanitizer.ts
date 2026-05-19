/**
 * wave-A1 client-side log sanitizer, extracted from the legacy
 * `LogsPage` paste box. After wave-A3 S4 the BACKEND sanitizer
 * (`LogSanitizer`) is the primary; this module stays as a
 * defence-in-depth pass on the live-tail wire payload (per the
 * slice contract: "Keep the wave-A1 sanitizer REGEXES as a
 * defense-in-depth pass on the client").
 */
type SanitizeRule =
  | { label: string; pattern: RegExp; kind: "string"; replacement: string }
  | { label: string; pattern: RegExp; kind: "fn"; replacement: (m: string, ...groups: string[]) => string };

const SANITIZE_RULES: SanitizeRule[] = [
  { label: "Authorization header", pattern: /Authorization:[^\r\n]+/gi, kind: "string", replacement: "Authorization: ***" },
  { label: "Cookie", pattern: /Cookie:\s*[^\r\n]+/gi, kind: "string", replacement: "Cookie: ***" },
  { label: "WIF key", pattern: /\b[5KL][1-9A-HJ-NP-Za-km-z]{50,51}\b/g, kind: "string", replacement: "***WIF***" },
  {
    label: "API key field (double-quoted)",
    pattern: /("?api[_-]?key"?\s*[:=]\s*)"[^"]+"/gi,
    kind: "fn",
    replacement: (_m, prefix) => `${prefix}"***"`,
  },
  {
    label: "API key field (single-quoted)",
    pattern: /('?api[_-]?key'?\s*[:=]\s*)'[^']+'/gi,
    kind: "fn",
    replacement: (_m, prefix) => `${prefix}'***'`,
  },
  {
    label: "Long hex blob",
    pattern: /\b[0-9a-fA-F]{128,}\b/g,
    kind: "fn",
    replacement: (m) => `${m.slice(0, 12)}…${m.slice(-12)}`,
  },
];

export interface SanitizeStat {
  rule: string;
  count: number;
}

export function sanitize(raw: string): { out: string; stats: SanitizeStat[] } {
  let out = raw;
  const stats: SanitizeStat[] = [];
  for (const r of SANITIZE_RULES) {
    const matches = out.match(r.pattern);
    const count = matches?.length ?? 0;
    if (count === 0) continue;
    out =
      r.kind === "string"
        ? out.replace(r.pattern, r.replacement)
        : out.replace(r.pattern, r.replacement);
    stats.push({ rule: r.label, count });
  }
  return { out, stats };
}
