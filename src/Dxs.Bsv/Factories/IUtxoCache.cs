using System.Collections.Generic;
using System.Threading.Tasks;

using Dxs.Bsv.Models;

namespace Dxs.Bsv.Factories;

public interface IUtxoCache
{
    bool IsUsed(OutPoint outPoint, out bool broadcasted);

    void MarkUsed(OutPoint outPoint, bool broadcasted);

    Task<List<OutPoint>> GetStasUtxos(ulong requestedSatoshis, Address address, TokenId tokenId);

    Task<OutPoint?> GetNextUtxoOrNull(Address address);
}
