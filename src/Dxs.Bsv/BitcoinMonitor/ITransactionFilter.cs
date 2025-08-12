using System;

namespace Dxs.Bsv.BitcoinMonitor;

public interface ITransactionFilter: IDisposable
{
    void ManageUtxoSetForAddress(Address address);
    void ManageUtxoSetForToken(TokenId tokenId);

    int QueueLength();
}