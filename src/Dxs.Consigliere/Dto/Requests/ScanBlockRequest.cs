using System.ComponentModel.DataAnnotations;

namespace Dxs.Consigliere.Dto.Requests;

public record ScanBlockRequest([Required] string BlockId);
