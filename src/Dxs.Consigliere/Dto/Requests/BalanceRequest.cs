using System.ComponentModel.DataAnnotations;

namespace Dxs.Consigliere.Dto.Requests;

public record BalanceRequest([Required] IList<string> Addresses, IList<string> TokenIds);
