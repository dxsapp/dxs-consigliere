using Dxs.Consigliere.Data.Models;

namespace Dxs.Consigliere.Services;

public interface IBlockDataProvider
{
    Task<int> ProcessBlock(BlockProcessContext context, CancellationToken cancellationToken);
}