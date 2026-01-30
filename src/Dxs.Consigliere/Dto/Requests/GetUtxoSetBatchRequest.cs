namespace Dxs.Consigliere.Dto.Requests;

public record GetUtxoSetBatchRequest(
    string[] TokenIds,
    string[] Addresses
);
