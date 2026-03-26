using Dxs.Bsv.Models;

namespace Dxs.Bsv.ScriptEvaluation;

public interface IPrevoutResolver
{
    bool TryResolve(string transactionId, uint vout, out OutPoint outPoint);
}
