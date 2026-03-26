namespace Dxs.Consigliere.Dto.Responses;

public sealed class StorageStatusResponse
{
    public RawTransactionPayloadStorageStatusResponse RawTransactionPayloads { get; set; } = new();
}

public sealed class RawTransactionPayloadStorageStatusResponse
{
    public bool Enabled { get; set; }
    public string Provider { get; set; } = string.Empty;
    public bool ProviderImplemented { get; set; }
    public bool PersistenceActive { get; set; }
    public string RetentionPolicy { get; set; } = string.Empty;
    public string Compression { get; set; } = string.Empty;
    public StorageLocationStatusResponse Location { get; set; } = new();
    public string[] Notes { get; set; } = [];
}

public sealed class StorageLocationStatusResponse
{
    public string Database { get; set; } = string.Empty;
    public string Collection { get; set; } = string.Empty;
    public string RootPath { get; set; } = string.Empty;
    public bool? ShardByTxId { get; set; }
    public string Bucket { get; set; } = string.Empty;
    public string Prefix { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
}
