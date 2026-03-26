#nullable enable

using Dxs.Common.Cache;
using Dxs.Consigliere.Dto.Requests;

namespace Dxs.Consigliere.Data.Cache;

public interface IProjectionReadCacheKeyFactory
{
    ProjectionCacheDescriptor CreateAddressHistory(GetAddressHistoryRequest request, string normalizedAddress, IReadOnlyCollection<string?> normalizedTokenIds);
    ProjectionCacheDescriptor CreateAddressBalances(IReadOnlyCollection<string> normalizedAddresses, IReadOnlyCollection<string>? normalizedTokenIds);
    ProjectionCacheDescriptor CreateAddressBatchUtxos(IReadOnlyCollection<string> normalizedAddresses, IReadOnlyCollection<string>? normalizedTokenIds, int take);
    ProjectionCacheDescriptor CreateAddressUtxos(string normalizedAddress, string? normalizedTokenId);
    ProjectionCacheDescriptor CreateTokenState(string normalizedTokenId);
    ProjectionCacheDescriptor CreateTokenUtxos(string normalizedTokenId);
    ProjectionCacheDescriptor CreateTokenBalances(string normalizedTokenId);
    ProjectionCacheDescriptor CreateTokenHistory(string normalizedTokenId, int take, int skip = 0, bool desc = true);
    ProjectionCacheDescriptor CreateTxLifecycle(string normalizedTxId);
    ProjectionCacheDescriptor CreateTrackedAddressReadiness(string normalizedAddress);
    ProjectionCacheDescriptor CreateTrackedTokenReadiness(string normalizedTokenId);
    IReadOnlyCollection<ProjectionCacheTag> GetAddressInvalidationTags(IEnumerable<string> addresses);
    IReadOnlyCollection<ProjectionCacheTag> GetTokenInvalidationTags(IEnumerable<string> tokenIds);
    IReadOnlyCollection<ProjectionCacheTag> GetTxLifecycleInvalidationTags(IEnumerable<string> txIds);
    IReadOnlyCollection<ProjectionCacheTag> GetTrackedAddressReadinessInvalidationTags(IEnumerable<string> addresses);
    IReadOnlyCollection<ProjectionCacheTag> GetTrackedTokenReadinessInvalidationTags(IEnumerable<string> tokenIds);
}

public sealed class ProjectionReadCacheKeyFactory : IProjectionReadCacheKeyFactory
{
    public ProjectionCacheDescriptor CreateAddressHistory(GetAddressHistoryRequest request, string normalizedAddress, IReadOnlyCollection<string?> normalizedTokenIds)
    {
        var tokens = CanonicalizeTokenSelection(normalizedTokenIds);
        return new ProjectionCacheDescriptor(
            new ProjectionCacheKey($"address-history|{normalizedAddress}|tokens={tokens}|desc={request.Desc}|skip-zero={request.SkipZeroBalance}|skip={request.Skip}|take={request.Take}"),
            [AddressHistoryTag(normalizedAddress)]);
    }

    public ProjectionCacheDescriptor CreateAddressBalances(IReadOnlyCollection<string> normalizedAddresses, IReadOnlyCollection<string>? normalizedTokenIds)
    {
        var addresses = Canonicalize(normalizedAddresses);
        var tokens = Canonicalize(normalizedTokenIds);
        return new ProjectionCacheDescriptor(
            new ProjectionCacheKey($"address-balances|addresses={addresses}|tokens={tokens}"),
            normalizedAddresses.Select(AddressBalanceTag).ToArray());
    }

    public ProjectionCacheDescriptor CreateAddressBatchUtxos(IReadOnlyCollection<string> normalizedAddresses, IReadOnlyCollection<string>? normalizedTokenIds, int take)
    {
        var addresses = Canonicalize(normalizedAddresses);
        var tokens = Canonicalize(normalizedTokenIds);
        return new ProjectionCacheDescriptor(
            new ProjectionCacheKey($"address-utxos-batch|addresses={addresses}|tokens={tokens}|take={take}"),
            normalizedAddresses.Select(AddressUtxoTag).ToArray());
    }

    public ProjectionCacheDescriptor CreateAddressUtxos(string normalizedAddress, string? normalizedTokenId)
        => new(
            new ProjectionCacheKey($"address-utxos|address={normalizedAddress}|token={NormalizeToken(normalizedTokenId)}"),
            [AddressUtxoTag(normalizedAddress)]);

    public ProjectionCacheDescriptor CreateTokenState(string normalizedTokenId)
        => new(
            new ProjectionCacheKey($"token-state|token={normalizedTokenId}"),
            [TokenStateTag(normalizedTokenId), TrackedTokenReadinessTag(normalizedTokenId)]);

    public ProjectionCacheDescriptor CreateTokenUtxos(string normalizedTokenId)
        => new(
            new ProjectionCacheKey($"token-utxos|token={normalizedTokenId}"),
            [TokenUtxoTag(normalizedTokenId), TrackedTokenReadinessTag(normalizedTokenId)]);

    public ProjectionCacheDescriptor CreateTokenBalances(string normalizedTokenId)
        => new(
            new ProjectionCacheKey($"token-balances|token={normalizedTokenId}"),
            [TokenBalanceTag(normalizedTokenId), TrackedTokenReadinessTag(normalizedTokenId)]);

    public ProjectionCacheDescriptor CreateTokenHistory(string normalizedTokenId, int take, int skip = 0, bool desc = true)
        => new(
            new ProjectionCacheKey($"token-history|token={normalizedTokenId}|skip={skip}|take={take}|desc={desc}"),
            [TokenHistoryTag(normalizedTokenId), TrackedTokenReadinessTag(normalizedTokenId)]);

    public ProjectionCacheDescriptor CreateTxLifecycle(string normalizedTxId)
        => new(
            new ProjectionCacheKey($"tx-lifecycle|tx={normalizedTxId}"),
            [TxLifecycleTag(normalizedTxId)]);

    public ProjectionCacheDescriptor CreateTrackedAddressReadiness(string normalizedAddress)
        => new(
            new ProjectionCacheKey($"tracked-address-readiness|address={normalizedAddress}"),
            [TrackedAddressReadinessTag(normalizedAddress)]);

    public ProjectionCacheDescriptor CreateTrackedTokenReadiness(string normalizedTokenId)
        => new(
            new ProjectionCacheKey($"tracked-token-readiness|token={normalizedTokenId}"),
            [TrackedTokenReadinessTag(normalizedTokenId)]);

    public IReadOnlyCollection<ProjectionCacheTag> GetAddressInvalidationTags(IEnumerable<string> addresses)
        => (addresses ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .SelectMany(address => new[]
            {
                AddressHistoryTag(address),
                AddressBalanceTag(address),
                AddressUtxoTag(address)
            })
            .ToArray();

    public IReadOnlyCollection<ProjectionCacheTag> GetTokenInvalidationTags(IEnumerable<string> tokenIds)
        => (tokenIds ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .SelectMany(tokenId => new[]
            {
                TokenStateTag(tokenId),
                TokenHistoryTag(tokenId),
                TokenBalanceTag(tokenId),
                TokenUtxoTag(tokenId)
            })
            .ToArray();

    public IReadOnlyCollection<ProjectionCacheTag> GetTxLifecycleInvalidationTags(IEnumerable<string> txIds)
        => (txIds ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .Select(TxLifecycleTag)
            .ToArray();

    public IReadOnlyCollection<ProjectionCacheTag> GetTrackedAddressReadinessInvalidationTags(IEnumerable<string> addresses)
        => (addresses ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .Select(TrackedAddressReadinessTag)
            .ToArray();

    public IReadOnlyCollection<ProjectionCacheTag> GetTrackedTokenReadinessInvalidationTags(IEnumerable<string> tokenIds)
        => (tokenIds ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .Select(TrackedTokenReadinessTag)
            .ToArray();

    private static ProjectionCacheTag AddressHistoryTag(string address) => new($"address-history:{address}");
    private static ProjectionCacheTag AddressBalanceTag(string address) => new($"address-balance:{address}");
    private static ProjectionCacheTag AddressUtxoTag(string address) => new($"address-utxo:{address}");
    private static ProjectionCacheTag TokenStateTag(string tokenId) => new($"token-state:{tokenId}");
    private static ProjectionCacheTag TokenHistoryTag(string tokenId) => new($"token-history:{tokenId}");
    private static ProjectionCacheTag TokenBalanceTag(string tokenId) => new($"token-balance:{tokenId}");
    private static ProjectionCacheTag TokenUtxoTag(string tokenId) => new($"token-utxo:{tokenId}");
    private static ProjectionCacheTag TxLifecycleTag(string txId) => new($"tx-lifecycle:{txId}");
    private static ProjectionCacheTag TrackedAddressReadinessTag(string address) => new($"tracked-address-readiness:{address}");
    private static ProjectionCacheTag TrackedTokenReadinessTag(string tokenId) => new($"tracked-token-readiness:{tokenId}");

    private static string Canonicalize(IEnumerable<string>? values)
        => string.Join(",",
            (values ?? [])
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(x => x, StringComparer.Ordinal));

    private static string CanonicalizeTokenSelection(IEnumerable<string?>? values)
        => string.Join(",",
            (values ?? [])
                .Select(NormalizeToken)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(x => x, StringComparer.Ordinal));

    private static string NormalizeToken(string? tokenId)
        => string.IsNullOrWhiteSpace(tokenId) ? "bsv" : tokenId;
}
