using System.Threading;
using System.Threading.Tasks;

using Dxs.Consigliere.WebSockets;

namespace Dxs.Consigliere.Services.P2p;

/// <summary>
/// Notifies SignalR subscribers when the headers chain advances to a
/// new tip. The interface is owned by Wave 1 S3; the production
/// implementation lives in S5 (`HubNewBlockNotifier`) and writes to the
/// `block:tip` group via <c>IHubContext&lt;WalletHub, IWalletHub&gt;</c>.
/// Default registration in tests / non-hub contexts is a no-op.
/// </summary>
public interface INewBlockNotifier
{
    Task NotifyAsync(BlockTipDto tip, CancellationToken ct);
}

/// <summary>Default no-op notifier — useful for tests and any non-hub host.</summary>
public sealed class NullNewBlockNotifier : INewBlockNotifier
{
    public Task NotifyAsync(BlockTipDto tip, CancellationToken ct) => Task.CompletedTask;
}
