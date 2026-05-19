using Dxs.Consigliere.Logging;

namespace Dxs.Consigliere.Tests.Logging;

/// <summary>
/// wave-A3 S4 — pins the ring-buffer + fan-out contract.
/// </summary>
public sealed class LogStreamBufferTests
{
    [Fact]
    public void Snapshot_returns_entries_in_publish_order()
    {
        var buffer = new LogStreamBuffer();
        buffer.Publish(Entry("first"));
        buffer.Publish(Entry("second"));
        var snapshot = buffer.Snapshot();
        Assert.Collection(snapshot,
            e => Assert.Equal("first", e.Message),
            e => Assert.Equal("second", e.Message));
    }

    [Fact]
    public void Drops_oldest_when_capacity_is_reached()
    {
        var buffer = new LogStreamBuffer();
        for (var i = 0; i < LogStreamBuffer.Capacity + 5; i++)
            buffer.Publish(Entry(i.ToString()));

        var snapshot = buffer.Snapshot();
        Assert.Equal(LogStreamBuffer.Capacity, snapshot.Length);
        // The oldest five entries were evicted.
        Assert.Equal("5", snapshot[0].Message);
        Assert.Equal((LogStreamBuffer.Capacity + 4).ToString(), snapshot[^1].Message);
    }

    [Fact]
    public void Subscribers_receive_published_entries()
    {
        var buffer = new LogStreamBuffer();
        var received = new List<LogEventDto>();
        using var sub = buffer.Subscribe(received.Add);
        buffer.Publish(Entry("one"));
        buffer.Publish(Entry("two"));
        Assert.Equal(2, received.Count);
    }

    [Fact]
    public void Disposing_subscription_stops_callbacks()
    {
        var buffer = new LogStreamBuffer();
        var received = new List<LogEventDto>();
        var sub = buffer.Subscribe(received.Add);
        buffer.Publish(Entry("one"));
        sub.Dispose();
        buffer.Publish(Entry("two"));
        Assert.Single(received);
    }

    [Fact]
    public void A_throwing_subscriber_does_not_break_the_publisher()
    {
        var buffer = new LogStreamBuffer();
        var good = new List<LogEventDto>();
        using var bad = buffer.Subscribe(_ => throw new InvalidOperationException("boom"));
        using var ok = buffer.Subscribe(good.Add);

        buffer.Publish(Entry("one"));
        buffer.Publish(Entry("two"));
        Assert.Equal(2, good.Count);
    }

    private static LogEventDto Entry(string message) => new()
    {
        UnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        Level = "information",
        Category = "Tests",
        Message = message,
    };
}
