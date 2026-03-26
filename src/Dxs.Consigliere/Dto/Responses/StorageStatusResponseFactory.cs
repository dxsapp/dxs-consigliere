using Dxs.Consigliere.Configs;

namespace Dxs.Consigliere.Dto.Responses;

public static class StorageStatusResponseFactory
{
    private const string ForeverRetention = "forever";

    public static StorageStatusResponse Build(ConsigliereStorageConfig storage)
    {
        var payloads = storage.RawTransactionPayloads ?? new RawTransactionPayloadsStorageConfig();
        var provider = payloads.Provider ?? string.Empty;
        var implemented = string.Equals(provider, "raven", StringComparison.OrdinalIgnoreCase);
        var notes = new List<string>();

        if (payloads.Enabled && !implemented)
            notes.Add("provider_not_implemented");

        return new StorageStatusResponse
        {
            RawTransactionPayloads = new RawTransactionPayloadStorageStatusResponse
            {
                Enabled = payloads.Enabled,
                Provider = provider,
                ProviderImplemented = implemented,
                PersistenceActive = payloads.Enabled && implemented,
                RetentionPolicy = ForeverRetention,
                Compression = payloads.Compression?.Enabled == true
                    ? payloads.Compression.Algorithm ?? string.Empty
                    : "disabled",
                Location = new StorageLocationStatusResponse
                {
                    Database = payloads.Location?.Database ?? string.Empty,
                    Collection = payloads.Location?.Collection ?? string.Empty,
                    RootPath = payloads.Location?.RootPath ?? string.Empty,
                    ShardByTxId = payloads.Location?.ShardByTxId,
                    Bucket = payloads.Location?.Bucket ?? string.Empty,
                    Prefix = payloads.Location?.Prefix ?? string.Empty,
                    Region = payloads.Location?.Region ?? string.Empty,
                    Endpoint = payloads.Location?.Endpoint ?? string.Empty
                },
                Notes = notes.ToArray()
            }
        };
    }
}
