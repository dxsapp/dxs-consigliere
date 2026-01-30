using Dxs.Bsv.Models;
using Dxs.Consigliere.Data;
using Dxs.Consigliere.Data.Models;

namespace Dxs.Consigliere.Services;

public interface IBroadcastService
{
    Task<decimal> SatoshisPerByte();

    Task<Broadcast> Broadcast(string raw, string batchId = null);
    Task<Broadcast> Broadcast(Transaction transaction, string batchId = null);
}
