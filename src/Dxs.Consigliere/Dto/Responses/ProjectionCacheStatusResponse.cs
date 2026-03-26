namespace Dxs.Consigliere.Dto.Responses;

public sealed class ProjectionCacheStatusResponse
{
    public bool Enabled { get; set; }
    public string Backend { get; set; }
    public int Count { get; set; }
    public int? MaxEntries { get; set; }
    public long Hits { get; set; }
    public long Misses { get; set; }
    public long FactoryCalls { get; set; }
    public long InvalidatedKeys { get; set; }
    public long InvalidatedTags { get; set; }
    public long Evictions { get; set; }
    public double HitRatio { get; set; }
}
