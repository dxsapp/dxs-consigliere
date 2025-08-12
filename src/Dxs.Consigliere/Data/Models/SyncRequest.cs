namespace Dxs.Consigliere.Data.Models;

public class SyncRequest
{
    public string Id { get; set; }

    public int FromHeight { get; set; }
    public int ToHeight { get; init; }
    public string SubscriptionId { get; init; }

    public DateTime? StartAt { get; set; }
    
    public bool Finished { get; set; }

    public HashSet<int> FailedBlocks { get; set; } = [];
}