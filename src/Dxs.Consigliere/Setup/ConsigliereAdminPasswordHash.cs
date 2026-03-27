using System.Security.Cryptography;

namespace Dxs.Consigliere.Setup;

public static class ConsigliereAdminPasswordHash
{
    private const string Prefix = "pbkdf2-sha256";
    private const int SaltSize = 16;
    private const int DerivedKeySize = 32;

    public static string Hash(string password, int iterations = 100_000)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(iterations);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var derived = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, DerivedKeySize);
        return string.Join(
            '$',
            Prefix,
            iterations.ToString(),
            Convert.ToBase64String(salt),
            Convert.ToBase64String(derived));
    }

    public static bool IsWellFormed(string encodedHash)
        => TryParse(encodedHash, out _, out _, out _);

    public static bool Verify(string password, string encodedHash)
    {
        if (string.IsNullOrEmpty(password))
            return false;

        if (!TryParse(encodedHash, out var iterations, out var salt, out var expected))
            return false;

        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    private static bool TryParse(
        string encodedHash,
        out int iterations,
        out byte[] salt,
        out byte[] expected)
    {
        iterations = 0;
        salt = [];
        expected = [];

        if (string.IsNullOrWhiteSpace(encodedHash))
            return false;

        var parts = encodedHash.Split('$', StringSplitOptions.TrimEntries);
        if (parts.Length != 4)
            return false;

        if (!string.Equals(parts[0], Prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!int.TryParse(parts[1], out iterations) || iterations <= 0)
            return false;

        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        return salt.Length > 0 && expected.Length > 0;
    }
}
