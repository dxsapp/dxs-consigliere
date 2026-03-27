namespace Dxs.Consigliere.Dto.Responses.Admin;

public sealed class AdminRealtimeSourcePolicyResponse
{
    public AdminRealtimeSourcePolicyValuesResponse Static { get; set; }
    public AdminRealtimeSourcePolicyValuesResponse Override { get; set; }
    public AdminRealtimeSourcePolicyValuesResponse Effective { get; set; }
    public bool OverrideActive { get; set; }
    public bool RestartRequired { get; set; }
    public string[] AllowedPrimarySources { get; set; } = [];
    public string[] AllowedBitailsTransports { get; set; } = [];
    public long? UpdatedAt { get; set; }
    public string UpdatedBy { get; set; }
}
