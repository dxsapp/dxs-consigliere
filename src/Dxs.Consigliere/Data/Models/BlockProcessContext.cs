namespace Dxs.Consigliere.Data.Models;

public class BlockProcessContext
{
    public string Id { get; set; }
    public int Height { get; set; }
    public DateTime? LastProcessAt { get; set; }
    public DateTime? NextProcessAt { get; set; }
    public long Timestamp { get; set; }
    public int ErrorsCount { get; set; }
    public List<string> Messages { get; } = new();
    public bool Orphaned { get; set; }
    public bool Scheduled { get; set; }
    public int Start { get; set; }
    public int Finish { get; set; }
    public int NotFound { get; set; }
    public int TransactionsCount { get; set; }
}