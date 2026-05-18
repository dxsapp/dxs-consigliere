using System.Threading.Tasks;

namespace Dxs.Bsv;

/// <summary>
/// Wave 5 S3: renamed from the now-removed <c>IBroadcastProvider</c>.
/// The interface used to combine fee estimation + broadcast, but
/// broadcast moved to the unified P2P path in
/// <c>IBroadcastService.BroadcastAsync</c> (Consigliere). The
/// remaining concern is fee-rate query, which downstream callers
/// (e.g. STAS transaction factories) still need.
/// </summary>
public interface IFeeRateProvider
{
    string Name { get; }

    Task<decimal> SatoshisPerByte();
}
