using System;

namespace Dxs.Infrastructure.Common;

public sealed record ExternalChainProviderHealthSnapshot(
    string Provider,
    ExternalChainHealthState State,
    string Detail = null,
    DateTimeOffset? ObservedAt = null
);
