using Dxs.Bsv.BitcoinMonitor;

namespace Dxs.Consigliere.Services;

public interface IMetaTransactionStore: ITransactionStore
{
    Task UpdateStasAttributes(string txId);
}