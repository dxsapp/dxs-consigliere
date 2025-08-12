using Newtonsoft.Json;

namespace Dxs.Infrastructure.Bitails.Dto;

public class BroadcastResponseDto
{
    public class BroadcastErrorDto
    {
        [JsonProperty("code")]
        public int Code { get; set; }
        
        [JsonProperty("message")]
        public string Message { get; set; }
    }
    
    [JsonProperty("txid")]
    public string TxId { get; set; }
    
    [JsonProperty("error")]
    public BroadcastErrorDto Error { get; set; } 
}