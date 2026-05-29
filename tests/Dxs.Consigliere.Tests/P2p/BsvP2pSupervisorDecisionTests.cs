using Dxs.Consigliere.Services.P2p;

namespace Dxs.Consigliere.Tests.P2p;

/// <summary>
/// wizard-enabled-p2p-runtime-toggle S2 — pins the supervisor's pure
/// start/stop decision: the actual peer-pool bring-up needs real network
/// I/O (operator-run / integration), but the converge logic (idempotency
/// + direction + CI-safe-off) is exercised here without touching the wire.
/// </summary>
public class BsvP2pSupervisorDecisionTests
{
    [Fact]
    public void Enabled_AndNotRunning_Starts()
        => Assert.Equal(BsvP2pHostedService.PoolAction.Start,
            BsvP2pHostedService.DecideAction(enabled: true, running: false));

    [Fact]
    public void Disabled_AndRunning_Stops()
        => Assert.Equal(BsvP2pHostedService.PoolAction.Stop,
            BsvP2pHostedService.DecideAction(enabled: false, running: true));

    [Fact]
    public void Enabled_AndAlreadyRunning_IsNoop_Idempotent()
        => Assert.Equal(BsvP2pHostedService.PoolAction.None,
            BsvP2pHostedService.DecideAction(enabled: true, running: true));

    [Fact]
    public void Disabled_AndNotRunning_IsNoop_CiSafeOff()
        => Assert.Equal(BsvP2pHostedService.PoolAction.None,
            BsvP2pHostedService.DecideAction(enabled: false, running: false));
}
