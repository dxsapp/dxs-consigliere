using Dxs.Bsv.BitcoinMonitor;
using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Consigliere.BackgroundTasks;
using Dxs.Consigliere.Configs;

using Microsoft.Extensions.Options;

namespace Dxs.Consigliere.Services.Impl;

public sealed class JournalFirstTxObservationSink(
    TxObservationJournalWriter journalWriter,
    IOptions<AppConfig> appConfig
) : ITxObservationSink
{
    public async Task RecordAsync(TxMessage message, CancellationToken cancellationToken = default)
    {
        if (!VNextCutoverMode.IsJournalFirst(appConfig.Value.VNextRuntime.CutoverMode))
            return;

        await journalWriter.AppendAsync(message, cancellationToken);
    }
}
