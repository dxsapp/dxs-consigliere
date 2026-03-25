using Dxs.Common.Journal;
using Dxs.Consigliere.Data.Models.Journal;
using Dxs.Consigliere.Extensions;

using Raven.Client.Documents;

namespace Dxs.Consigliere.Data.Journal;

public sealed class RavenJournalSequenceAllocator(
    IDocumentStore documentStore
) : IJournalSequenceAllocator
{
    public async ValueTask<JournalSequence> AllocateAsync(CancellationToken cancellationToken = default)
    {
        using var session = documentStore.GetClusterSession();
        var state = await session.LoadAsync<ObservationJournalSequenceState>(
            ObservationJournalSequenceState.DocumentId,
            cancellationToken
        );

        if (state is null)
        {
            state = new ObservationJournalSequenceState
            {
                Id = ObservationJournalSequenceState.DocumentId,
                LastAllocatedSequence = JournalSequence.Empty.Next().Value
            };

            await session.StoreAsync(state, state.Id, cancellationToken);
        }
        else
        {
            state.LastAllocatedSequence = new JournalSequence(state.LastAllocatedSequence).Next().Value;
        }

        await session.SaveChangesAsync(cancellationToken);

        return new JournalSequence(state.LastAllocatedSequence);
    }
}
