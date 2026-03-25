namespace Dxs.Common.Journal;

/// <summary>
/// Stable semantic identity for an observation journal entry.
/// </summary>
public readonly record struct DedupeFingerprint
{
    public string Value { get; }

    public DedupeFingerprint(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Fingerprint cannot be empty.", nameof(value));
        }

        Value = value.Trim();
    }

    public override string ToString() => Value;
}
