using System.Runtime.Serialization;

namespace Dxs.Infrastructure.JungleBus.Dto;

public class PubControlMessageDto
{
    /// <summary>
    /// 1	connecting	Sent when connecting to the JungleBus server
    /// 2	connected	Sent when connected to the JungleBus server
    /// 10	disconnecting	Sent when disconnecting from the JungleBus server
    /// 11	disconnected	Sent when disconnected from the JungleBus server
    /// 20	subscribing	Sent when subscribing on a subscription on the JungleBus server
    /// 21	subscribed	Sent when subscribed on a subscription on the JungleBus server
    /// 29	unsubscribed	Sent when unsubscribed on a subscription on the JungleBus server
    /// 100	waiting	A ping message that is sent to indicate waiting for a new block
    /// 101	subscription_error	Sent when an error occurs on a subscription
    /// 200	block_done	Sent when all transaction of a block have been processed and sent
    /// 300	reorg	Sent when a reorg has occurred
    /// 999	error	Sent when a generic error occurs
    /// </summary>
    [DataMember(Name = "statusCode")]
    public int Code { get; set; }

    [DataMember(Name = "status")]
    public string Status { get; set; }

    [DataMember(Name = "message")]
    public string Message { get; set; }

    [DataMember(Name = "block")]
    public int Block { get; set; }

    [DataMember(Name = "transactions")]
    public int TransactionCount { get; set; }

    public enum StatusCode
    {
        Connecting = 1,
        Connected = 2,
        Disconnecting = 10,
        Disconnected = 11,
        Subscribing = 20,
        Subscribed = 21,
        Unsubscribed = 29,
        Waiting = 100,
        SubscriptionError = 101,
        BlockDone = 200,
        Reorg = 300,
        Error = 999,
    }
}
