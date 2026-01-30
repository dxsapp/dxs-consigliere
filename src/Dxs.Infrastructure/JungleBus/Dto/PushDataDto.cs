using System.Runtime.Serialization;

namespace Dxs.Infrastructure.JungleBus.Dto;

public class PushDataDto<TPubData>
{
    [DataMember(Name = "data")]
    public TPubData Data { get; set; }
}
