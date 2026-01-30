using System.Collections.Generic;
using System.Threading.Tasks;

using Dxs.Bsv.Models;
using Dxs.Bsv.Transactions.Build;

namespace Dxs.Bsv.Tokens;

public interface ITokenTransactionFactory
{
    Task<TransactionBuilder> CreateContract(
        ITokenSchema schema,
        IList<Payment> tokenPayments,
        Address contractOwnerAddress,
        ulong satoshisToToken,
        Payment feePayment
    );

    Task<TransactionBuilder> Issue(
        ITokenSchema schema,
        IList<Destination> destinations,
        Payment contractPayment,
        Payment feePayment,
        params byte[][] data
    );

    Task<TransactionBuilder> Transfer(
        Destination destination,
        Payment tokenPayment,
        Payment feePayment,
        ITokenSchema tokenSchema = null,
        params byte[][] data
    );

    /// <summary>
    /// split will take an existing STAS UTXO and assign it to up to 4 addresses. The tokenOwnerPrivateKey must own the existing STAS UTXO.
    /// the paymentPrivateKey owns the paymentUtxo and will be the owner of any change from the fee.
    /// </summary>
    Task<TransactionBuilder> Split(
        IList<Destination> destinations,
        Payment tokenPayment,
        Payment feePayment,
        params byte[][] data
    );

    Task<TransactionBuilder> Merge(
        OutPoint stasOutPoint1,
        OutPoint stasOutPoint2,
        PrivateKey senderPrivateKey,
        Payment feePayment,
        Destination destination1,
        Destination? destination2 = null,
        params byte[][] data
    );

    Task<TransactionBuilder> Redeem(
        ITokenSchema schema,
        Payment tokenPayment,
        Payment feePayment,
        IList<Destination> splitDestinations,
        params byte[][] data
    );
}
