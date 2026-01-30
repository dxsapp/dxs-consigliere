namespace Dxs.Bsv.Rpc.Models.Responses;

public class RpcResponseString : RpcResponseBase<string>;

public class RpcResponseStringWithErrorDetails : RpcResponseBase<string, CodeAndMessageErrorResponse>;
