using System.Collections.Generic;

namespace Dxs.Infrastructure.Common;

public sealed record ExternalChainProviderDescriptor(
    string Provider,
    IReadOnlyCollection<string> Capabilities,
    ExternalChainRateLimitHint RateLimitHint = null
);
