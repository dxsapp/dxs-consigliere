using Swashbuckle.AspNetCore.Annotations;

namespace Dxs.Consigliere.Dto.Requests;

/// <summary>
/// 
/// </summary>
/// <param name="TokenId">Optional</param>
/// <param name="Address"></param>
/// <param name="Satoshis">Optional; If presented api will return minimal utxo set greater than or equal requested satoshis</param>
public record GetUtxoSetRequest(
    string TokenId,
    string Address,
    [SwaggerParameter("Optional. If specified ")]
    long? Satoshis
): AddressBaseRequest(Address);