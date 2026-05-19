using System.Text.RegularExpressions;

namespace Dxs.Consigliere.Logging;

/// <summary>
/// wave-A3 S4 — server-side log sanitizer. Ports the regex set
/// from the wave-A1 admin-ui paste-box (the client-side rules)
/// to the BACKEND so the wire payload is already-clean by the
/// time it reaches the admin UI. The slice contract puts the
/// server-side pass as the primary sanitizer; the client keeps
/// the same rules as a defence-in-depth second layer.
///
/// Rules cover:
/// - Authorization / Cookie headers (redact through EOL)
/// - WIF private keys (BSV / testnet)
/// - JSON apiKey / api_key / apikey fields (double + single
///   quoted variants)
/// - hex blobs ≥ 128 chars (head + tail kept)
/// </summary>
public static partial class LogSanitizer
{
    [GeneratedRegex(@"Authorization:[^\r\n]+", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex AuthorizationHeaderRegex();

    [GeneratedRegex(@"Cookie:\s*[^\r\n]+", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex CookieHeaderRegex();

    [GeneratedRegex(@"\b[5KL][1-9A-HJ-NP-Za-km-z]{50,51}\b", RegexOptions.Compiled)]
    private static partial Regex WifRegex();

    [GeneratedRegex(@"(""?api[_-]?key""?\s*[:=]\s*)""[^""]+""", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex ApiKeyDoubleRegex();

    [GeneratedRegex(@"('?api[_-]?key'?\s*[:=]\s*)'[^']+'", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex ApiKeySingleRegex();

    [GeneratedRegex(@"\b[0-9a-fA-F]{128,}\b", RegexOptions.Compiled)]
    private static partial Regex LongHexRegex();

    public static string Apply(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return raw;
        var result = AuthorizationHeaderRegex().Replace(raw, "Authorization: ***");
        result = CookieHeaderRegex().Replace(result, "Cookie: ***");
        result = WifRegex().Replace(result, "***WIF***");
        result = ApiKeyDoubleRegex().Replace(result, m => $"{m.Groups[1].Value}\"***\"");
        result = ApiKeySingleRegex().Replace(result, m => $"{m.Groups[1].Value}'***'");
        result = LongHexRegex().Replace(result, m => $"{m.Value[..12]}…{m.Value[^12..]}");
        return result;
    }
}
