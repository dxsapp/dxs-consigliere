using System.Collections.Generic;
using System.Threading.Tasks;

using Dxs.Bsv.Factories.Models;
using Dxs.Bsv.Models;
using Dxs.Bsv.Tokens;

namespace Dxs.Bsv.Factories;

public interface IStasBundleTransactionFactory
{
    Task<List<PreparedTransaction>> BuildStasTransactionsBundle(
        ITokenSchema tokenSchema,
        PrivateKey fromKey,
        PrivateKey feeKey,
        Address to,
        ulong satoshisToSend,
        List<OutPoint> utxos,
        OutPoint fundingOutPoint,
        ulong minSatoshisToSplit,
        params byte[][] data
    );
}
