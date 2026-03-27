namespace Dxs.Consigliere.Dto.Responses.Admin;

public sealed class AdminTrackedEntityDeleteResponse
{
    public string EntityType { get; set; }
    public string EntityId { get; set; }
    public string Code { get; set; }
    public bool Tombstoned { get; set; }
    public long? TombstonedAt { get; set; }
}
