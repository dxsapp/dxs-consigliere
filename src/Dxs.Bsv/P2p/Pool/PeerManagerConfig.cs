#nullable enable
using System;

using Dxs.Bsv.P2p.Messages;
using Dxs.Bsv.P2p.Session;

namespace Dxs.Bsv.P2p.Pool;

public sealed record PeerManagerConfig
{
    public int TargetPoolSize { get; init; } = 8;

    /// <summary>Maximum simultaneous bootstrap-stage handshakes. Audit H3 fix.</summary>
    public int BootstrapMaxConcurrency { get; init; } = 4;

    /// <summary>Random jitter applied between bootstrap attempts.</summary>
    public TimeSpan BootstrapJitter { get; init; } = TimeSpan.FromMilliseconds(200);

    /// <summary>How long a failed peer stays in cooldown before retry.</summary>
    public TimeSpan NegativeCooldown { get; init; } = TimeSpan.FromMinutes(15);

    /// <summary>Pool maintenance tick interval.</summary>
    public TimeSpan MaintenanceInterval { get; init; } = TimeSpan.FromSeconds(15);

    /// <summary>How often to refresh DNS seeds.</summary>
    public TimeSpan DnsRefreshInterval { get; init; } = TimeSpan.FromHours(6);

    /// <summary>Per-session knobs (handshake timeout etc.).</summary>
    public PeerSessionConfig SessionConfig { get; init; } = new();

    /// <summary>Factory for the <c>version</c> message we send to peers. Caller provides; we cache.</summary>
    public Func<VersionMessage> VersionFactory { get; init; } = () => throw new InvalidOperationException("PeerManagerConfig.VersionFactory must be set");

    /// <summary>
    /// Seed the store with hardcoded fallback peers from <see cref="P2pNetwork.FallbackSeeds"/>
    /// on <see cref="PeerManager.StartAsync"/>. Default <c>true</c> — gives a bootstrap
    /// path when DNS seeds return only banned nodes. Tests should set <c>false</c>.
    /// </summary>
    public bool EnableFallbackSeeds { get; init; } = true;
}
