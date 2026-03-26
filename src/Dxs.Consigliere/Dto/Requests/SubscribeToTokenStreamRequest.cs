using System.ComponentModel.DataAnnotations;

namespace Dxs.Consigliere.Dto.Requests;

public record SubscribeToTokenStreamRequest([Required] string TokenId);
