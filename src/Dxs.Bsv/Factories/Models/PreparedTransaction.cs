using System.Collections.Generic;
using Dxs.Bsv.Models;

namespace Dxs.Bsv.Factories.Models;

public struct PreparedTransaction
{
    public Transaction Transaction { get; init; }
    public List<OutPoint> UsedOutPoints { get; init; }

    public ulong Fee { get; init; }
    public long FeeSize { get; init; }
}