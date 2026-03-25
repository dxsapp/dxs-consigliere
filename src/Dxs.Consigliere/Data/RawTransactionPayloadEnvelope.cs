namespace Dxs.Consigliere.Data;

public sealed record RawTransactionPayloadEnvelope(
    RawTransactionPayloadReference Reference,
    string PayloadHex
);
