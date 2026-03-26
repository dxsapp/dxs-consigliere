namespace Dxs.Infrastructure.Common;

public static class ExternalChainCapability
{
    public const string Broadcast = "broadcast";
    public const string RealtimeIngest = "realtime_ingest";
    public const string BlockBackfill = "block_backfill";
    public const string RawTxFetch = "raw_tx_fetch";
    public const string ValidationFetch = "validation_fetch";
    public const string HistoricalAddressScan = "historical_address_scan";
    public const string HistoricalTokenScan = "historical_token_scan";
}
