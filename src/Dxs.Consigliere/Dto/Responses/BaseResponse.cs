namespace Dxs.Consigliere.Dto.Responses;

public record BaseResponse(bool Success, string Message)
{
    public static BaseResponse SuccessResponse() => new(true, null);

    public static BaseResponse FailureResponse(string message) => new(false, message);
}