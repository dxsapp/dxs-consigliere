namespace Dxs.Consigliere.Dto.Responses;

public record UtxoSetResponse(IEnumerable<UtxoDto> UtxoSet, bool Success, string Message) : BaseResponse(Success, Message)
{
    public static UtxoSetResponse SuccessResponse(IEnumerable<UtxoDto> utxoSet) => new(utxoSet, true, null);

    public new static UtxoSetResponse FailureResponse(string message) => new(Array.Empty<UtxoDto>(), true, message);
}
