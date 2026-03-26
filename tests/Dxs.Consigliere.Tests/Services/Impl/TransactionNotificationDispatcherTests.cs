using Dxs.Bsv;
using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Bsv.Models;
using Dxs.Bsv.Script;
using Dxs.Common.Interfaces;
using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Services.Impl;
using Dxs.Consigliere.WebSockets;

using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Moq;

namespace Dxs.Consigliere.Tests.Services.Impl;

public class TransactionNotificationDispatcherTests
{
    private const string Address = "1A1zP1eP5QGefi2DMPTfTL5SLmv7DivfNa";

    [Fact]
    public async Task OnTransactionFound_PushesLegacyNotification_WhenLegacyCutoverModeIsActive()
    {
        var hub = new Mock<IWalletHub>(MockBehavior.Strict);
        hub.Setup(x => x.OnTransactionFound(It.IsAny<string>())).Returns(Task.CompletedTask);

        var clients = new Mock<IHubClients<IWalletHub>>(MockBehavior.Strict);
        clients.Setup(x => x.Group(Address)).Returns(hub.Object);

        var groups = new Mock<IGroupManager>(MockBehavior.Loose);
        var context = new Mock<IHubContext<WalletHub, IWalletHub>>(MockBehavior.Strict);
        context.SetupGet(x => x.Clients).Returns(clients.Object);
        context.SetupGet(x => x.Groups).Returns(groups.Object);

        var dispatcher = new TransactionNotificationDispatcher(
            context.Object,
            new FakeAppCache(),
            Options.Create(new AppConfig
            {
                VNextRuntime = new VNextRuntimeConfig
                {
                    CutoverMode = VNextCutoverMode.Legacy
                }
            }),
            NullLogger<TransactionNotificationDispatcher>.Instance
        );

        await dispatcher.OnTransactionFound(CreateMessage());

        hub.Verify(x => x.OnTransactionFound(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task OnTransactionFound_DoesNotPushLegacyNotification_WhenJournalFirstModeIsActive()
    {
        var hub = new Mock<IWalletHub>(MockBehavior.Strict);
        var clients = new Mock<IHubClients<IWalletHub>>(MockBehavior.Strict);
        var groups = new Mock<IGroupManager>(MockBehavior.Loose);
        var context = new Mock<IHubContext<WalletHub, IWalletHub>>(MockBehavior.Strict);
        context.SetupGet(x => x.Clients).Returns(clients.Object);
        context.SetupGet(x => x.Groups).Returns(groups.Object);

        var dispatcher = new TransactionNotificationDispatcher(
            context.Object,
            new FakeAppCache(),
            Options.Create(new AppConfig
            {
                VNextRuntime = new VNextRuntimeConfig
                {
                    CutoverMode = VNextCutoverMode.VNextDefault
                }
            }),
            NullLogger<TransactionNotificationDispatcher>.Instance
        );

        await dispatcher.OnTransactionFound(CreateMessage());

        clients.Verify(x => x.Group(It.IsAny<string>()), Times.Never);
        hub.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task OnAddressBalanceChanged_DoesNotPushLegacyBalance_WhenJournalFirstModeIsActive()
    {
        var hub = new Mock<IWalletHub>(MockBehavior.Strict);
        var clients = new Mock<IHubClients<IWalletHub>>(MockBehavior.Strict);
        var groups = new Mock<IGroupManager>(MockBehavior.Loose);
        var context = new Mock<IHubContext<WalletHub, IWalletHub>>(MockBehavior.Strict);
        context.SetupGet(x => x.Clients).Returns(clients.Object);
        context.SetupGet(x => x.Groups).Returns(groups.Object);

        var dispatcher = new TransactionNotificationDispatcher(
            context.Object,
            new FakeAppCache(),
            Options.Create(new AppConfig
            {
                VNextRuntime = new VNextRuntimeConfig
                {
                    CutoverMode = VNextCutoverMode.ShadowRead
                }
            }),
            NullLogger<TransactionNotificationDispatcher>.Instance
        );

        await dispatcher.OnAddressBalanceChanged(Address);

        clients.Verify(x => x.Group(It.IsAny<string>()), Times.Never);
        hub.VerifyNoOtherCalls();
    }

    private static FilteredTransactionMessage CreateMessage()
    {
        var tx = new Transaction(Network.Mainnet)
        {
            Id = new string('a', 64),
            Raw = [0x01, 0x02]
        };
        tx.Outputs.Add(new Output
        {
            Address = new Address(Address),
            Type = ScriptType.P2PKH,
            Satoshis = 1,
            Idx = 0,
            ScriptPubKey = default
        });

        return new FilteredTransactionMessage(tx, [Address]);
    }

    private sealed class FakeAppCache : IAppCache<ConnectionManager>
    {
        private readonly Dictionary<string, object?> _items = new(StringComparer.Ordinal);

        public int Count => _items.Count;

        public bool TryGet<T>(string key, out T value)
        {
            if (_items.TryGetValue(key, out var raw) && raw is T typed)
            {
                value = typed;
                return true;
            }

            value = default!;
            return false;
        }

        public T Get<T>(string key) => _items.TryGetValue(key, out var raw) && raw is T typed ? typed : default!;

        public void Set<T>(string key, T item, TimeSpan? relativeExpiration = null) => _items[key] = item;

        public void Set<T>(string key, T item, DateTime absolutExpiration) => _items[key] = item;

        public T GetOrAdd<T>(string key, Func<T> addItemFactory, TimeSpan? relativeExpiration = null, TimeSpan? slidingExpiration = null)
        {
            if (TryGet<T>(key, out var existing))
                return existing;

            var created = addItemFactory();
            _items[key] = created;
            return created;
        }

        public Task<T> GetOrAddAsync<T>(string key, Func<ICacheEntry, Task<T>> addItemFactory)
            => throw new NotSupportedException();

        public Task<T> GetOrAddAsync<T>(string key, Func<Task<T>> addItemFactory, TimeSpan? relativeExpiration = null, TimeSpan? slidingExpiration = null)
            => throw new NotSupportedException();

        public Task<T> GetOrAddAsync<T>(string key, Func<Task<T>> addItemFactory, DateTimeOffset? absoluteExpiration)
            => throw new NotSupportedException();

        public void Remove(string key) => _items.Remove(key);
    }
}
