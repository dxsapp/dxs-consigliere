namespace Dxs.Consigliere.Configs;

public class ConsigliereStorageConfig
{
    public RawTransactionPayloadsStorageConfig RawTransactionPayloads { get; set; } = new();
}

public class RawTransactionPayloadsStorageConfig
{
    public bool Enabled { get; set; }
    public string Provider { get; set; }
    public RawTransactionPayloadLocationConfig Location { get; set; } = new();
    public PayloadCompressionConfig Compression { get; set; }
}

// The active payload location shape is provider-specific and keyed by Provider.
// Keeping all optional fields in one object allows us to bind the vnext config
// contract now without forcing a storage implementation choice in this slice.
public class RawTransactionPayloadLocationConfig
{
    public string Database { get; set; }
    public string Collection { get; set; }
    public string RootPath { get; set; }
    public bool? ShardByTxId { get; set; }
    public string Bucket { get; set; }
    public string Prefix { get; set; }
    public string Region { get; set; }
    public string Endpoint { get; set; }
}

public class PayloadCompressionConfig
{
    public bool Enabled { get; set; }
    public string Algorithm { get; set; }
}
