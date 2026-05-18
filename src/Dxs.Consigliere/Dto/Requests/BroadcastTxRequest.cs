namespace Dxs.Consigliere.Dto.Requests;

/// <summary>
/// Wave 5 S1 — body shape for <c>POST /api/tx/broadcast</c>. The
/// canonical broadcast entrypoint replaces the legacy URL-segment
/// shape (<c>POST /api/tx/broadcast/{raw}</c>). Wire format is
/// <c>{ "rawHex": "..." }</c> (ASP.NET Core default camelCase).
/// </summary>
public sealed record BroadcastTxRequest(string RawHex);
