namespace Dxs.Consigliere.Dto.Responses.Admin;

public sealed class AdminFindingResponse
{
    public string EntityType { get; set; }
    public string EntityId { get; set; }
    public string Code { get; set; }
    public string Severity { get; set; }
    public string Message { get; set; }
    public long? ObservedAt { get; set; }
}
