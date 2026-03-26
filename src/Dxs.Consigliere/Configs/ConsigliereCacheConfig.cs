#nullable enable

namespace Dxs.Consigliere.Configs;

public sealed class ConsigliereCacheConfig
{
    public bool Enabled { get; set; } = true;
    public string Backend { get; set; } = "memory";
    public int MaxEntries { get; set; } = 10_000;
    public int? SafetyTtlSeconds { get; set; }
    public AzosProjectionCacheConfig Azos { get; set; } = new();
}

public sealed class AzosProjectionCacheConfig
{
    public bool Enabled { get; set; }
    public string? AssemblyPath { get; set; }
    public string? TableName { get; set; }
}
