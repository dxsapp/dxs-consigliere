using Dxs.Consigliere.Data;
using Dxs.Consigliere.Data.Journal;
using Dxs.Consigliere.Extensions;
using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Common.Journal;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Raven.Migrations;

namespace Dxs.Consigliere.Setup;

public static class PersistenceSetup
{
    public static IServiceCollection AddPersistenceZoneServices(
        this IServiceCollection services,
        IConfiguration configuration
    )
        => services
            .Configure<RavenDbConfig>(configuration.GetSection("RavenDb"))
            .AddSingleton<RavenDbDocumentStore>()
            .AddRavenDbMigrations(options =>
            {
                options.PreventSimultaneousMigrations = true;
                options.SimultaneousMigrationTimeout = TimeSpan.FromMinutes(30);
            })
            .AddSingleton(sp => sp.GetRequiredService<RavenDbDocumentStore>().DocumentStore)
            .AddSingleton<IRawTransactionPayloadStore, RavenRawTransactionPayloadStore>()
            .AddSingleton<IObservationJournalAppender<ObservationJournalEntry<TxObservation>>, RavenObservationJournal<TxObservation>>()
            .AddSingleton<IObservationJournalAppender<ObservationJournalEntry<BlockObservation>>, RavenObservationJournal<BlockObservation>>()
            .AddScoped(sp => sp.GetRequiredService<RavenDbDocumentStore>().DocumentStore.GetSession());
}
