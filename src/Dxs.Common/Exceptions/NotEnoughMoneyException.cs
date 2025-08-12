using Dxs.Common.Extensions;

namespace Dxs.Common.Exceptions.Transactions;

public class NotEnoughMoneyException(
    ISet<string> fromAddresses,
    decimal amountAvailable,
    string toAddress,
    decimal amountNeeded,
    decimal fee,
    string tokenId
) : Exception(BuildMessage(fromAddresses, amountAvailable, toAddress, amountNeeded, fee, tokenId))
{
    public ISet<string> FromAddresses { get; } = fromAddresses;
    public decimal AmountAvailable { get; } = amountAvailable;
    public string ToAddress { get; } = toAddress;
    public decimal AmountNeeded { get; } = amountNeeded;
    public string TokenId { get; } = tokenId;

    private static string BuildMessage(
        ISet<string> fromAddresses,
        decimal amountAvailable,
        string toAddress,
        decimal amountNeeded,
        decimal fee,
        string tokenId
    ) => tokenId.IsNullOrEmpty()
        ? $"Not enough money ({amountAvailable} / {amountNeeded} + {fee}) on [{string.Join(',', fromAddresses)}] to send to [{toAddress}]."
        : $"Not enough money ({amountAvailable} / {amountNeeded} + {fee}) on [{string.Join(',', fromAddresses)}] to send to [{toAddress}]. TokenId: {tokenId}";
}