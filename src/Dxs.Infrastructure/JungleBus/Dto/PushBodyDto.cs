using System.Runtime.Serialization;

namespace Dxs.Infrastructure.JungleBus.Dto;

public class PushBodyDto<TPubData>
{
    [DataMember(Name = "channel")]
    public string Channel { get; set; }

    [DataMember(Name = "pub")]
    public PushDataDto<TPubData> Pub { get; set; }
}
