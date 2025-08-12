using System.Runtime.Serialization;

namespace Dxs.Infrastructure.JungleBus.Dto;

public class ClientDto
{
    [DataMember(Name = "client")]
    public string Client { get; set; }

    [DataMember(Name = "ping")]
    public int Ping { get; set; }

    [DataMember(Name = "pong")]
    public bool Pong { get; set; }
}