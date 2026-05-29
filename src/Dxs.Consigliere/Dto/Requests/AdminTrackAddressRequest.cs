using Dxs.Consigliere.Data.Models.Tracking;

namespace Dxs.Consigliere.Dto.Requests;

/// <summary>
/// Operator request to start tracking (watching) an address. Defaults to
/// <see cref="TrackedEntityHistoryMode.ForwardOnly"/> — watch from now on,
/// no history backfill — which is the right mode for exercising the P2P
/// mempool watchlist filter without triggering a heavy historical sync.
/// </summary>
public sealed class AdminTrackAddressRequest
{
    public string Address { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string HistoryMode { get; set; } = TrackedEntityHistoryMode.ForwardOnly;
}
