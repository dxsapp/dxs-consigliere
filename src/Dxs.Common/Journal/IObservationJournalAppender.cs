namespace Dxs.Common.Journal;

/// <summary>
/// Appends observations to a durable journal.
/// </summary>
public interface IObservationJournalAppender<TObservation>
{
    ValueTask<ObservationJournalAppendResult> AppendAsync(
        ObservationJournalAppendRequest<TObservation> request,
        CancellationToken cancellationToken = default);
}
