using Dxs.Common.Journal;

namespace Dxs.Consigliere.Data.Models.Journal;

public class ObservationJournalRecordDocument
{
    public string Id { get; set; } = string.Empty;
    public long Sequence { get; set; }
    public string Fingerprint { get; set; } = string.Empty;
    public string ObservationType { get; set; } = string.Empty;
    public string ObservationJson { get; set; } = string.Empty;
    public string PayloadDocumentId { get; set; }
    public string PayloadTxId { get; set; }
    public string PayloadCompressionAlgorithm { get; set; }
    public DateTimeOffset AppendedAt { get; set; }

    public static string GetId(JournalSequence sequence) => $"journal/records/{sequence.Value:D20}";
}
