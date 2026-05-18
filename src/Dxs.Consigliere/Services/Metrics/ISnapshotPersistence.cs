#nullable enable
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Dxs.Consigliere.Data.Models.Metrics;

namespace Dxs.Consigliere.Services.Metrics;

/// <summary>
/// Wave 4 A2 M3 fix — thin persistence abstraction over the
/// snapshot-doc operations. Lets the aggregator be unit-tested
/// without standing up a full Raven LINQ mock; the production
/// implementation <see cref="RavenSnapshotPersistence"/> wraps
/// the actual IDocumentStore session calls.
/// </summary>
public interface ISnapshotPersistence
{
    Task StoreAsync(SourceMetricsSnapshot snapshot, CancellationToken cancellationToken);

    /// <summary>Returns every retained snapshot id in
    /// lexicographic (= time) order (oldest first).</summary>
    Task<IReadOnlyList<string>> GetAllIdsOrderedAsync(CancellationToken cancellationToken);

    /// <summary>Deletes the given snapshot ids in one batch.</summary>
    Task DeleteAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken);
}
