using Dxs.Consigliere.Logging;

namespace Dxs.Consigliere.Tests.Logging;

/// <summary>
/// wave-A3 S4 — pins every rule the wave-A1 admin-ui paste box
/// sanitizer carried. A regression here means a secret could
/// reach the wire log-stream payload.
/// </summary>
public sealed class LogSanitizerTests
{
    [Fact]
    public void Authorization_header_redacted_through_end_of_line()
    {
        var input = "GET /api/foo\nAuthorization: Bearer abc.def.ghi\nX-Other: keep";
        var output = LogSanitizer.Apply(input);
        Assert.Contains("Authorization: ***", output);
        Assert.DoesNotContain("abc.def.ghi", output);
        Assert.Contains("X-Other: keep", output);
    }

    [Fact]
    public void Cookie_header_redacted()
    {
        var input = "Cookie: consigliere_admin=zzz; other=keep";
        var output = LogSanitizer.Apply(input);
        Assert.Contains("Cookie: ***", output);
        Assert.DoesNotContain("zzz", output);
    }

    [Fact]
    public void Wif_private_key_redacted()
    {
        // 52-char mainnet WIF (starts with L).
        const string wif = "L4rK1yDtCWekvXuE6oXD9jCYfFNV2cWRpVuPLBcCU2z8TrisoyY1";
        var input = $"signing with {wif}";
        Assert.Contains("***WIF***", LogSanitizer.Apply(input));
    }

    [Fact]
    public void Double_quoted_api_key_field_redacted_preserving_prefix()
    {
        var input = "{\"apiKey\":\"some-secret-token\"}";
        var output = LogSanitizer.Apply(input);
        Assert.Equal("{\"apiKey\":\"***\"}", output);
    }

    [Fact]
    public void Single_quoted_api_key_field_redacted_preserving_prefix()
    {
        var input = "{ 'api_key': 'some-secret' }";
        var output = LogSanitizer.Apply(input);
        Assert.Equal("{ 'api_key': '***' }", output);
    }

    [Fact]
    public void Long_hex_blob_truncated_to_head_and_tail()
    {
        var blob = new string('a', 200);
        var input = $"raw: {blob} done";
        var output = LogSanitizer.Apply(input);
        Assert.DoesNotContain(blob, output);
        Assert.Contains("…", output);
    }

    [Fact]
    public void Short_strings_are_left_alone()
    {
        const string clean = "ready in 12ms — 42 peers connected";
        Assert.Equal(clean, LogSanitizer.Apply(clean));
    }
}
