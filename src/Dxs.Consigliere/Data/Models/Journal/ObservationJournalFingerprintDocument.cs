using Dxs.Common.Journal;

namespace Dxs.Consigliere.Data.Models.Journal;

public class ObservationJournalFingerprintDocument
{
    public string Id { get; set; } = string.Empty;
    public long Sequence { get; set; }
    public string JournalRecordId { get; set; } = string.Empty;

    public static string GetId(DedupeFingerprint fingerprint) => $"journal/fingerprints/{fingerprint.Value}";
}
