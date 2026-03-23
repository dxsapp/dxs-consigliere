using System.Collections.Concurrent;
using System.Collections.Generic;

using Dxs.Bsv.Models;
using Dxs.Bsv.Script;
using Dxs.Common.Extensions;

namespace Dxs.Bsv.BitcoinMonitor.Impl;

internal sealed class TransactionFilterWatchSet
{
    private readonly ConcurrentDictionary<string, Address> _watchingAddresses = new();
    private readonly ConcurrentDictionary<string, TokenId> _watchingTokens = new();
    private readonly ConcurrentDictionary<string, Address> _watchingTokensRedeemAddresses = new();

    public int WatchingAddressesCount => _watchingAddresses.Count;
    public int WatchingTokensCount => _watchingTokens.Count;

    public void AddAddress(Address address)
        => _watchingAddresses.TryAdd(address.Value, address);

    public void AddToken(TokenId tokenId)
    {
        _watchingTokens.TryAdd(tokenId.Value, tokenId);
        _watchingTokensRedeemAddresses.TryAdd(tokenId.RedeemAddress.Value, tokenId.RedeemAddress);
    }

    public void SeedAddresses(IEnumerable<Address> addresses)
    {
        foreach (var address in addresses)
            AddAddress(address);
    }

    public void SeedTokens(IEnumerable<TokenId> tokenIds)
    {
        foreach (var tokenId in tokenIds)
            AddToken(tokenId);
    }

    public TransactionFilterMatchResult Match(Transaction transaction)
    {
        var addresses = new HashSet<string>();
        var save = false;
        string redeemAddress = null;

        foreach (var output in transaction.Outputs)
        {
            if (output.Address?.Value is { } address)
            {
                if (_watchingAddresses.ContainsKey(address))
                {
                    save = true;
                    addresses.Add(address);
                }

                if (output.Idx == 0 && _watchingTokensRedeemAddresses.ContainsKey(address))
                {
                    save = true;
                    redeemAddress = address;
                }
            }

            if (output.Type is ScriptType.P2STAS or ScriptType.DSTAS &&
                output.TokenId.IsNotNullOrEmpty() &&
                _watchingTokens.ContainsKey(output.TokenId))
            {
                save = true;

                if (output.Address != null)
                    addresses.Add(output.Address.Value);
            }
        }

        foreach (var input in transaction.Inputs)
        {
            if (input.Address == null) continue;

            if (!save && _watchingAddresses.ContainsKey(input.Address.Value))
                save = true;

            addresses.Add(input.Address.Value);
        }

        return new TransactionFilterMatchResult(save, redeemAddress, addresses.Count > 0 ? addresses : null);
    }
}

internal sealed record TransactionFilterMatchResult(bool Save, string RedeemAddress, HashSet<string> Addresses);
