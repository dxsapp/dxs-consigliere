using Dxs.Bsv;
using Dxs.Bsv.Models;

namespace Dxs.Consigliere.Services;

/// <summary>
/// Wave 5 S3: legacy <c>IBroadcastProvider</c> base interface was
/// renamed to <see cref="IFeeRateProvider"/> (broadcast moved to the
/// unified P2P path in <see cref="IBroadcastService.BroadcastAsync"/>;
/// the surviving concern is fee-rate query). The bitcoind / RPC
/// surface retains fee estimation + mempool queries used by
/// historical-data backfill jobs.
/// </summary>
public interface IBitcoindService : IFeeRateProvider
{
    Task<IList<Transaction>> GetMempoolTransactions();
}
