#nullable enable

using Dxs.Consigliere.Data.Models.Addresses;
using Dxs.Consigliere.Data.Models.Transactions;

namespace Dxs.Consigliere.Data.Addresses;

internal static class AddressHistoryEnvelopeHelper
{
    public static bool HasHistoryEnvelope(AddressProjectionAppliedTransactionDocument application)
        => application.Timestamp.HasValue
            && application.Height.HasValue
            && application.ValidStasTx.HasValue
            && application.TxFeeSatoshis.HasValue
            && application.Note is not null
            && application.FromAddresses is not null
            && application.ToAddresses is not null;

    public static void Hydrate(
        AddressProjectionAppliedTransactionDocument application,
        MetaTransaction transaction,
        IReadOnlyCollection<AddressProjectionUtxoSnapshot> debits,
        IReadOnlyCollection<AddressProjectionUtxoSnapshot> credits
    )
    {
        application.Timestamp = transaction.Timestamp;
        application.Height = transaction.Height;
        application.ValidStasTx = transaction.IsIssue
            ? transaction.IsValidIssue
            : !transaction.IllegalRoots.Any();
        application.Note = transaction.Note;
        application.TxFeeSatoshis = debits.Sum(x => x.Satoshis) - credits.Sum(x => x.Satoshis);
        application.FromAddresses = debits
            .Select(x => x.Address)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(16)
            .ToArray();
        application.ToAddresses = credits
            .Select(x => x.Address)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(16)
            .ToArray();
    }
}
