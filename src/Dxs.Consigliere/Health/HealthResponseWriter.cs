using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Dxs.Consigliere.Health;

/// <summary>
/// wave-A3 S2 — single JSON shape that all three endpoints emit:
/// <code>{status, checks: [{name, status, description, durationMs}]}</code>
///
/// The shape is intentionally documented here so a future
/// wave-A4 UI surface can deserialise it without coupling to
/// `Microsoft.Extensions.Diagnostics.HealthChecks` internals.
///
/// <c>description</c> is whatever the check returned — it is the
/// individual check's responsibility to keep it terse + free of
/// hostnames / stack traces / secrets (the endpoints are
/// anonymous, per the wave-A3 launch constraints).
/// </summary>
internal static class HealthResponseWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static Task Write(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        var payload = new
        {
            status = report.Status.ToString().ToLowerInvariant(),
            checks = report.Entries.Select(kvp => new
            {
                name = kvp.Key,
                status = kvp.Value.Status.ToString().ToLowerInvariant(),
                description = kvp.Value.Description,
                durationMs = (long)kvp.Value.Duration.TotalMilliseconds
            }).ToArray()
        };

        return JsonSerializer.SerializeAsync(context.Response.Body, payload, JsonOptions);
    }
}
