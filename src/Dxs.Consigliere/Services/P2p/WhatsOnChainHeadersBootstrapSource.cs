#nullable enable
using System;
using System.Buffers.Binary;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Dxs.Bsv.P2p.Chain;
using Dxs.Bsv.P2p.Messages;

using Microsoft.Extensions.Logging;

namespace Dxs.Consigliere.Services.P2p;

/// <summary>
/// Wave 1 S4 default <see cref="IHeadersBootstrapSource"/> backed by
/// WhatsOnChain's public API:
/// - GET /v1/bsv/main/chain/info → height + bestblockhash (display)
/// - GET /v1/bsv/main/block/{hash}/header → JSON block-header object
///   with version / previousblockhash / merkleroot / time / bits /
///   nonce fields. The 80-byte raw header is reconstructed from those
///   fields and the result is verified by recomputing the hash and
///   matching against bestblockhash.
///
/// Self-contained <see cref="HttpClient"/> so we don't have to extend
/// the existing <c>external-chain-adapters</c> surface during W1. If
/// any step fails, returns null and the bootstrapper falls back to
/// pure-P2P cold start — which is now fail-closed: the chain stays
/// <see cref="ExtendResult.Unanchored"/> and arbitrary P2P headers are
/// dropped rather than promoted to height 0.
///
/// Added per audit A2 H1; revised per audit A2-followup new-H1 to
/// consume the actual live WoC JSON shape.
/// </summary>
public sealed class WhatsOnChainHeadersBootstrapSource : IHeadersBootstrapSource, IDisposable
{
    private const string BaseUrl = "https://api.whatsonchain.com/v1/bsv/main/";
    private readonly HttpClient _http;
    private readonly ILogger<WhatsOnChainHeadersBootstrapSource> _logger;
    private readonly bool _ownsClient;

    public WhatsOnChainHeadersBootstrapSource(ILogger<WhatsOnChainHeadersBootstrapSource> logger)
    {
        _logger = logger;
        _http = new HttpClient
        {
            BaseAddress = new Uri(BaseUrl),
            Timeout = TimeSpan.FromSeconds(10),
        };
        _ownsClient = true;
    }

    /// <summary>Constructor for tests / DI injection of a pre-configured client.</summary>
    public WhatsOnChainHeadersBootstrapSource(HttpClient http, ILogger<WhatsOnChainHeadersBootstrapSource> logger)
    {
        _http = http;
        _logger = logger;
        _ownsClient = false;
    }

    public async Task<BootstrapSeed?> FetchAsync(CancellationToken ct)
    {
        try
        {
            // 1. chain/info → tip height + bestblockhash (display order).
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
            var bestBlockHashDisplay = hashEl.GetString();
            if (string.IsNullOrEmpty(bestBlockHashDisplay))
            {
                _logger.LogWarning("WoC /chain/info returned empty bestblockhash");
                return null;
            }

            // 2. /block/{hash}/header → JSON block-header object.
            using var headerResp = await _http.GetAsync($"block/{bestBlockHashDisplay}/header", ct);
            if (!headerResp.IsSuccessStatusCode)
            {
                _logger.LogWarning("WoC /block/{Hash}/header returned {Status}",
                    bestBlockHashDisplay, headerResp.StatusCode);
                return null;
            }
            var headerJson = await headerResp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            var headerBytes = TryBuildHeaderBytes(headerJson, _logger);
            if (headerBytes is null) return null;

            // 3. Self-check: recompute hash, must match bestblockhash.
            var computed = BlockHeaderHasher.ToDisplayHex(
                BlockHeaderHasher.Hash(new BlockHeader(headerBytes)));
            if (!string.Equals(computed, bestBlockHashDisplay.ToLowerInvariant(), StringComparison.Ordinal))
            {
                _logger.LogWarning("WoC header reconstruction hash mismatch: expected {Exp} got {Got}",
                    bestBlockHashDisplay, computed);
                return null;
            }

            _logger.LogInformation("WoC bootstrap fetched tip height={H} hash={Hash}", height, bestBlockHashDisplay);
            return new BootstrapSeed(headerBytes, height);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogWarning(ex, "WoC bootstrap fetch failed");
            return null;
        }
    }

    /// <summary>
    /// Reconstruct the 80-byte BSV block header from the WoC JSON
    /// object. Returns null when any required field is missing or
    /// malformed. Public for test access.
    /// </summary>
    public static byte[]? TryBuildHeaderBytes(JsonElement json, ILogger logger)
    {
        try
        {
            if (!TryReadUInt32(json, "version", out var version)) { logger.LogWarning("WoC header: missing version"); return null; }
            if (!TryReadUInt32(json, "time", out var time)) { logger.LogWarning("WoC header: missing time"); return null; }
            if (!TryReadUInt32(json, "nonce", out var nonce)) { logger.LogWarning("WoC header: missing nonce"); return null; }
            if (!TryReadBits(json, out var bits)) { logger.LogWarning("WoC header: missing/bad bits"); return null; }
            if (!TryReadMerkle(json, out var merkleRoot)) { logger.LogWarning("WoC header: missing/bad merkleroot"); return null; }
            var prevBlock = ReadPrevBlockOrZero(json);

            var bytes = new byte[BlockHeader.Size];
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0, 4), version);
            Buffer.BlockCopy(prevBlock, 0, bytes, 4, 32);
            Buffer.BlockCopy(merkleRoot, 0, bytes, 36, 32);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(68, 4), time);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(72, 4), bits);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(76, 4), nonce);
            return bytes;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "WoC header reconstruction failed");
            return null;
        }
    }

    private static bool TryReadUInt32(JsonElement obj, string name, out uint value)
    {
        value = 0;
        if (!obj.TryGetProperty(name, out var el)) return false;
        if (el.ValueKind == JsonValueKind.Number)
        {
            if (el.TryGetInt64(out var n) && n >= 0 && n <= uint.MaxValue) { value = (uint)n; return true; }
        }
        if (el.ValueKind == JsonValueKind.String && uint.TryParse(el.GetString(), out var s)) { value = s; return true; }
        return false;
    }

    private static bool TryReadBits(JsonElement obj, out uint value)
    {
        value = 0;
        if (!obj.TryGetProperty("bits", out var el)) return false;
        // WoC returns bits as a hex string (display order = big-endian
        // representation of the compact uint, e.g. "1d00ffff" for the
        // genesis difficulty). Parse → uint, then the caller writes it
        // little-endian into the header bytes.
        if (el.ValueKind == JsonValueKind.String)
        {
            var s = el.GetString();
            if (string.IsNullOrEmpty(s)) return false;
            return uint.TryParse(s, System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture, out value);
        }
        if (el.ValueKind == JsonValueKind.Number)
        {
            if (el.TryGetInt64(out var n) && n >= 0 && n <= uint.MaxValue) { value = (uint)n; return true; }
        }
        return false;
    }

    private static bool TryReadMerkle(JsonElement obj, out byte[] wireOrder)
    {
        wireOrder = Array.Empty<byte>();
        if (!obj.TryGetProperty("merkleroot", out var el)) return false;
        var s = el.GetString();
        if (string.IsNullOrEmpty(s) || s.Length != 64) return false;
        var displayBytes = Convert.FromHexString(s);
        wireOrder = ReverseBytes(displayBytes);
        return true;
    }

    private static byte[] ReadPrevBlockOrZero(JsonElement obj)
    {
        // Genesis has no previousblockhash; treat as 32 zero bytes.
        if (!obj.TryGetProperty("previousblockhash", out var el)) return new byte[32];
        var s = el.GetString();
        if (string.IsNullOrEmpty(s) || s.Length != 64) return new byte[32];
        var displayBytes = Convert.FromHexString(s);
        return ReverseBytes(displayBytes);
    }

    private static byte[] ReverseBytes(byte[] src)
    {
        var dst = new byte[src.Length];
        for (var i = 0; i < src.Length; i++) dst[i] = src[src.Length - 1 - i];
        return dst;
    }

    public void Dispose()
    {
        if (_ownsClient) _http.Dispose();
    }
}
