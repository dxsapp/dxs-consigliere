using System.Collections.Generic;

using Dxs.Bsv.Models;

namespace Dxs.Bsv.BitcoinMonitor.Models;

public readonly struct FilteredTransactionMessage
{
    public FilteredTransactionMessage(Transaction transaction, HashSet<string> addresses)
        : this(transaction, addresses, default)
    {
    }

    public FilteredTransactionMessage(Transaction transaction, HashSet<string> addresses, TxMessage sourceMessage)
    {
        Transaction = transaction;
        Addresses = addresses;
        SourceMessage = sourceMessage;
    }

    public Transaction Transaction { get; }
    public HashSet<string> Addresses { get; }
    public TxMessage SourceMessage { get; }
}
