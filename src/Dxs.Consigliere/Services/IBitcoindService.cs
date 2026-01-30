using Dxs.Bsv;
using Dxs.Bsv.Models;

namespace Dxs.Consigliere.Services;

public interface IBitcoindService : IBroadcastProvider
{
    Task<IList<Transaction>> GetMempoolTransactions();
}
