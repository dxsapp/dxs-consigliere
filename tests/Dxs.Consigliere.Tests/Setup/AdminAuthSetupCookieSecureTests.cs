using Dxs.Consigliere.Setup;
using Microsoft.AspNetCore.Http;

namespace Dxs.Consigliere.Tests.Setup;

/// <summary>
/// wave-A3 S0 — pins the cookie SecurePolicy parser. The parser
/// is conservative by default: any unknown / null value maps to
/// `Always`, so a misconfiguration cannot silently drop the
/// Secure flag in production.
/// </summary>
public sealed class AdminAuthSetupCookieSecureTests
{
    [Theory]
    [InlineData("Always", CookieSecurePolicy.Always)]
    [InlineData("always", CookieSecurePolicy.Always)]
    [InlineData("ALWAYS", CookieSecurePolicy.Always)]
    [InlineData("SameAsRequest", CookieSecurePolicy.SameAsRequest)]
    [InlineData("sameasrequest", CookieSecurePolicy.SameAsRequest)]
    [InlineData("None", CookieSecurePolicy.None)]
    [InlineData("none", CookieSecurePolicy.None)]
    public void Parses_Known_Values(string input, CookieSecurePolicy expected)
    {
        Assert.Equal(expected, AdminAuthSetup.ParseCookieSecurePolicy(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("nope")]
    [InlineData("0")]
    public void Defaults_Unknown_Values_To_Always(string? input)
    {
        Assert.Equal(CookieSecurePolicy.Always, AdminAuthSetup.ParseCookieSecurePolicy(input));
    }
}
