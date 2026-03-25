namespace Dxs.Consigliere.Benchmarks.Replay;

public sealed class ReplayHarness
{
    public ReplayMetrics Execute(ReplayScenario scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);

        var txCount = 0;
        var blockCount = 0;
        var lastSequence = 0L;

        foreach (var observation in scenario.Observations)
        {
            lastSequence = observation.Sequence;

            switch (observation.EventType)
            {
                case ReplayEventType.TxSeenBySource:
                case ReplayEventType.TxSeenInMempool:
                case ReplayEventType.TxSeenInBlock:
                case ReplayEventType.TxDroppedBySource:
                    txCount++;
                    break;
                case ReplayEventType.BlockConnected:
                case ReplayEventType.BlockDisconnected:
                    blockCount++;
                    break;
            }
        }

        return new ReplayMetrics(
            scenario.Observations.Count,
            txCount,
            blockCount,
            lastSequence);
    }
}
