namespace Dxs.Common.Journal;

/// <summary>
/// Allocates the next monotonic sequence in a journal stream.
/// </summary>
public interface IJournalSequenceAllocator
{
    ValueTask<JournalSequence> AllocateAsync(CancellationToken cancellationToken = default);
}
