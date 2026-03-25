namespace Dxs.Consigliere.Data;

public static class RawTransactionPayloadCompressionAlgorithm
{
    public const string None = "none";
    public const string Gzip = "gzip";
    public const string Zstd = "zstd";

    public static bool IsSupported(string algorithm)
        => algorithm is None or Gzip or Zstd;
}
