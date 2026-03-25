namespace Dxs.Infrastructure.Common;

public sealed record ExternalChainRateLimitHint(
    int? RequestsPerMinute,
    string SourceHint = null
);
