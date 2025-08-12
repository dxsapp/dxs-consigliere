using System.Runtime.Serialization;

namespace Dxs.Infrastructure.JungleBus.Dto;

public class ConnectDto
{
    [DataMember(Name = "id")]
    public int Id { get; set; }

    [DataMember(Name = "connect")]
    public ClientDto Data { get; set; }
}