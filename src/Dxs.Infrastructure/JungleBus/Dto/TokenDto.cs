using System.Runtime.Serialization;

namespace Dxs.Infrastructure.JungleBus.Dto;

public class TokenDto
{
    [DataMember(Name = "token")]
    public string Token { get; set; }
}