using System.Collections.Generic;
using System.Threading.Tasks;
using Dxs.Bsv.Factories.Models;
using Dxs.Bsv.Models;

namespace Dxs.Bsv.Factories;

public interface IP2PkhTransactionFactory
{
    Task<PreparedTransaction> BuildP2PkhTransaction(
        IReadOnlyList<PrivateKey> fromKeys,
        IReadOnlyList<Destination> destinations,
        FeeType feeType,
        params byte[][] notes
    );
}