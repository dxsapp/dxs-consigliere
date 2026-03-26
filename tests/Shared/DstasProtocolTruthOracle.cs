using System.Security.Cryptography;
using System.Text.Json;

namespace Dxs.Tests.Shared;

public static class DstasProtocolTruthOracle
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly Lazy<DstasProtocolTruthOracleManifest> Manifest = new(LoadManifestCore);

    public static DstasProtocolTruthOracleManifest LoadManifest() => Manifest.Value;

    public static string ResolveValidatedPath(string fixtureId)
    {
        var fixture = GetFixture(fixtureId);
        var path = RepoPathResolver.ResolveFromRepoRoot(fixture.Path.Split('/'));
        ValidateFixtureHash(fixture, path);
        return path;
    }

    public static IReadOnlyList<DstasProtocolTruthOracleValidationResult> ValidateAll()
    {
        var results = new List<DstasProtocolTruthOracleValidationResult>();
        foreach (var fixture in LoadManifest().Fixtures)
        {
            var path = RepoPathResolver.ResolveFromRepoRoot(fixture.Path.Split('/'));
            ValidateFixtureHash(fixture, path);
            results.Add(new DstasProtocolTruthOracleValidationResult(fixture.Id, path, fixture.Sha256));
        }

        foreach (var mirror in LoadManifest().Mirrors)
        {
            var left = RepoPathResolver.ResolveFromRepoRoot(GetFixture(mirror.PrimaryFixtureId).Path.Split('/'));
            var right = RepoPathResolver.ResolveFromRepoRoot(GetFixture(mirror.MirrorFixtureId).Path.Split('/'));
            if (!File.ReadAllBytes(left).AsSpan().SequenceEqual(File.ReadAllBytes(right)))
                throw new InvalidOperationException($"DSTAS protocol oracle mirror mismatch: {mirror.PrimaryFixtureId} != {mirror.MirrorFixtureId}");
        }

        return results;
    }

    private static DstasProtocolTruthOracleFixture GetFixture(string fixtureId)
        => LoadManifest().Fixtures.FirstOrDefault(x => string.Equals(x.Id, fixtureId, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"Missing DSTAS protocol oracle fixture '{fixtureId}'.");

    private static void ValidateFixtureHash(DstasProtocolTruthOracleFixture fixture, string path)
    {
        if (!File.Exists(path))
            throw new InvalidOperationException($"DSTAS protocol oracle fixture file is missing: {path}");

        var hash = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
        if (!string.Equals(hash, fixture.Sha256, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"DSTAS protocol oracle hash mismatch for '{fixture.Id}'. Expected '{fixture.Sha256}', got '{hash}'.");
        }
    }

    private static DstasProtocolTruthOracleManifest LoadManifestCore()
    {
        var path = RepoPathResolver.ResolveFromRepoRoot(
            "tests",
            "Shared",
            "fixtures",
            "dstas-protocol-truth-oracle.json");

        if (!File.Exists(path))
            throw new InvalidOperationException($"Missing DSTAS protocol truth oracle manifest: {path}");

        var manifest = JsonSerializer.Deserialize<DstasProtocolTruthOracleManifest>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize DSTAS protocol truth oracle manifest.");

        if (manifest.Fixtures.Count == 0)
            throw new InvalidOperationException("DSTAS protocol truth oracle manifest contains no fixtures.");

        return manifest;
    }
}

public sealed class DstasProtocolTruthOracleManifest
{
    public string Version { get; set; } = string.Empty;
    public string ExportedAt { get; set; } = string.Empty;
    public string Contract { get; set; } = string.Empty;
    public List<DstasProtocolTruthOracleFixture> Fixtures { get; set; } = [];
    public List<DstasProtocolTruthOracleMirror> Mirrors { get; set; } = [];
}

public sealed class DstasProtocolTruthOracleFixture
{
    public string Id { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
    public string Origin { get; set; } = string.Empty;
}

public sealed class DstasProtocolTruthOracleMirror
{
    public string PrimaryFixtureId { get; set; } = string.Empty;
    public string MirrorFixtureId { get; set; } = string.Empty;
}

public sealed record DstasProtocolTruthOracleValidationResult(string FixtureId, string Path, string Sha256);
