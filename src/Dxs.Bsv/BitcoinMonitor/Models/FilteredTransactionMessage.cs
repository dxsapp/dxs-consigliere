using System.Collections.Generic;
using Dxs.Bsv.Models;

namespace Dxs.Bsv.BitcoinMonitor.Models;

public readonly struct FilteredTransactionMessage(Transaction transaction, HashSet<string> addresses)
{
    public Transaction Transaction { get; } = transaction;
    public HashSet<string> Addresses { get; } = addresses;
}