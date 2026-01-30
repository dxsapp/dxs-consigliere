using System.Runtime.Serialization;

namespace Dxs.Infrastructure.JungleBus.Dto;

public class PushDto<TPubData>
{
    [DataMember(Name = "push")]
    public PushBodyDto<TPubData> Push { get; set; }
}
