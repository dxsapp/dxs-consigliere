using Dxs.Consigliere.Data.Models.Tracking;

namespace Dxs.Consigliere.Dto.Requests;

/// <summary>
/// Operator request to start tracking (watching) a token. Defaults to
/// <see cref="TrackedEntityHistoryMode.ForwardOnly"/>. <see cref="TrustedRoots"/>
/// only applies to full-history tracking; it is ignored for forward-only.
/// </summary>
public sealed class AdminTrackTokenRequest
{
    public string TokenId { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public string HistoryMode { get; set; } = TrackedEntityHistoryMode.ForwardOnly;
    public string[] TrustedRoots { get; set; } = [];
}
