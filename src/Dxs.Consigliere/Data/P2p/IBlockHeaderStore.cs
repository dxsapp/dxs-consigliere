using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Dxs.Consigliere.Data.Models.P2p;

namespace Dxs.Consigliere.Data.P2p;

/// <summary>
/// Storage surface for <see cref="BlockHeaderDocument"/>. Production
/// binding is <see cref="BlockHeaderStore"/> (Raven). Tests / spikes
/// may substitute an in-memory implementation — this keeps the
/// HeadersChainService consumable without an embedded Raven runtime
/// (added per audit A2 H2).
/// </summary>
public interface IBlockHeaderStore
{
    Task SaveAsync(BlockHeaderDocument doc, CancellationToken ct = default);
    Task<BlockHeaderDocument> GetByHashAsync(string hashHex, CancellationToken ct = default);
    Task<BlockHeaderDocument> GetTipAsync(CancellationToken ct = default);
    Task<IReadOnlyList<BlockHeaderDocument>> RecentAsync(int count, CancellationToken ct = default);
    Task PruneBelowAsync(long minHeight, CancellationToken ct = default);
}
