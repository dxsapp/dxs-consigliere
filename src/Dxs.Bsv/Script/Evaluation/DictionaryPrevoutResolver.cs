using System.Collections.Generic;
using System.Linq;

using Dxs.Bsv.Models;

namespace Dxs.Bsv.ScriptEvaluation;

public sealed class DictionaryPrevoutResolver : IPrevoutResolver
{
    private readonly IReadOnlyDictionary<(string TxId, uint Vout), OutPoint> _prevouts;

    public DictionaryPrevoutResolver(IEnumerable<OutPoint> prevouts)
    {
        _prevouts = prevouts.ToDictionary(x => (x.TransactionId, x.Vout));
    }

    public bool TryResolve(string transactionId, uint vout, out OutPoint outPoint)
        => _prevouts.TryGetValue((transactionId, vout), out outPoint);
}
