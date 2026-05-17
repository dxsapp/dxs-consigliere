#nullable enable
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

namespace Dxs.Consigliere.Services.P2p;

/// <summary>
/// Wave 1 S4. Default <see cref="IHeadersBootstrapSource"/> backed by
/// WhatsOnChain's public API:
/// - GET /v1/bsv/main/chain/info → tip hash (display order) + height
/// - GET /v1/bsv/main/block/{hash}/header → 80-byte raw header (hex)
///
/// Self-contained <see cref="HttpClient"/> so we don't have to extend
/// the existing <c>external-chain-adapters</c> surface during W1. If
/// either call fails, returns null and the bootstrapper falls back to
/// pure-P2P cold start — which is now safe because the chain refuses
/// to auto-promote arbitrary headers (returns
/// <see cref="Dxs.Bsv.P2p.Chain.ExtendResult.Unanchored"/>).
///
/// Added per audit A2 H1.
/// </summary>
public sealed class WhatsOnChainHeadersBootstrapSource : IHeadersBootstrapSource, IDisposable
{
    private const string BaseUrl = "https://api.whatsonchain.com/v1/bsv/main/";
    private readonly HttpClient _http;
    private readonly ILogger<WhatsOnChainHeadersBootstrapSource> _logger;

    public WhatsOnChainHeadersBootstrapSource(ILogger<WhatsOnChainHeadersBootstrapSource> logger)
    {
        _logger = logger;
        _http = new HttpClient
        {
            BaseAddress = new Uri(BaseUrl),
            Timeout = TimeSpan.FromSeconds(10),
        };
    }

    /// <summary>Constructor for tests / DI injection of a pre-configured client.</summary>
    public WhatsOnChainHeadersBootstrapSource(HttpClient http, ILogger<WhatsOnChainHeadersBootstrapSource> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<BootstrapSeed?> FetchAsync(CancellationToken ct)
    {
        try
        {
            using var chainResp = await _http.GetAsync("chain/info", ct);
            if (!chainResp.IsSuccessStatusCode)
            {
                _logger.LogWarning("WoC /chain/info returned {Status}", chainResp.StatusCode);
                return null;
            }
            var chainInfo = await chainResp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            if (!chainInfo.TryGetProperty("blocks", out var blocksEl) ||
                !chainInfo.TryGetProperty("bestblockhash", out var hashEl))
            {
                _logger.LogWarning("WoC /chain/info missing required fields");
                return null;
            }
            var height = blocksEl.GetInt64();
            var displayHash = hashEl.GetString();
            if (string.IsNullOrEmpty(displayHash))
            {
                _logger.LogWarning("WoC /chain/info returned empty bestblockhash");
                return null;
            }

            // /block/{display-hash}/header returns the 160-char (80-byte) raw header hex.
            using var headerResp = await _http.GetAsync($"block/{displayHash}/header", ct);
            if (!headerResp.IsSuccessStatusCode)
            {
                _logger.LogWarning("WoC /block/{Hash}/header returned {Status}",
                    displayHash, headerResp.StatusCode);
                return null;
            }
            // WoC returns either a raw hex string or a JSON object with a "hex" field
            // depending on the network/endpoint variant. Try both.
            var raw = await headerResp.Content.ReadAsStringAsync(ct);
            var rawHex = ExtractHeaderHex(raw);
            if (rawHex is null || rawHex.Length != 160)
            {
                _logger.LogWarning("WoC header response unparseable (len={Len})", rawHex?.Length ?? 0);
                return null;
            }
            var headerBytes = Convert.FromHexString(rawHex);
            _logger.LogInformation("WoC bootstrap fetched tip height={H} hash={Hash}", height, displayHash);
            return new BootstrapSeed(headerBytes, height);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "WoC bootstrap fetch failed");
            return null;
        }
    }

    private static string? ExtractHeaderHex(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;
        var trimmed = body.Trim();
        // Plain hex (with or without quotes).
        if (trimmed.StartsWith('"') && trimmed.EndsWith('"'))
            trimmed = trimmed[1..^1];
        if (trimmed.Length == 160 && IsHex(trimmed)) return trimmed.ToLowerInvariant();
        // JSON object — look for "hex" or "raw" field.
        try
        {
            var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("hex", out var hex)) return hex.GetString()?.ToLowerInvariant();
            if (doc.RootElement.TryGetProperty("raw", out var raw)) return raw.GetString()?.ToLowerInvariant();
        }
        catch (JsonException) { /* fall through */ }
        return null;
    }

    private static bool IsHex(string s)
    {
        foreach (var c in s)
        {
            if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
                return false;
        }
        return true;
    }

    public void Dispose() => _http.Dispose();
}
