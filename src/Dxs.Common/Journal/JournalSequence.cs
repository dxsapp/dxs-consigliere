using System.Globalization;

namespace Dxs.Common.Journal;

/// <summary>
/// Monotonic journal sequence number.
/// </summary>
public readonly record struct JournalSequence : IComparable<JournalSequence>
{
    public long Value { get; }

    public JournalSequence(long value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Sequence cannot be negative.");
        }

        Value = value;
    }

    public static JournalSequence Empty => new(0);

    public bool HasValue => Value > 0;

    public JournalSequence Next() => new(checked(Value + 1));

    public int CompareTo(JournalSequence other) => Value.CompareTo(other.Value);

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
