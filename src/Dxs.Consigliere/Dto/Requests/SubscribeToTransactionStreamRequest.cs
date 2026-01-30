using System.ComponentModel.DataAnnotations;

namespace Dxs.Consigliere.Dto.Requests;

public record SubscribeToTransactionStreamRequest([Required] string Address, bool Slim);
