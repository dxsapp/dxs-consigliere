using Dxs.Consigliere.Data.Models;

namespace Dxs.Consigliere.Data.Models.Runtime;

public sealed class RealtimeSourcePolicyOverrideDocument : AuditableEntity
{
    public const string DocumentId = "operator/runtime/realtime-source-policy";

    public string PrimaryRealtimeSource { get; set; }
    public string RawTxPrimaryProvider { get; set; }
    public string RestPrimaryProvider { get; set; }
    public string BitailsTransport { get; set; }
    public string BitailsApiKey { get; set; }
    public string BitailsBaseUrl { get; set; }
    public string BitailsWebsocketBaseUrl { get; set; }
    public string BitailsZmqTxUrl { get; set; }
    public string BitailsZmqBlockUrl { get; set; }
    public string WhatsonchainApiKey { get; set; }
    public string WhatsonchainBaseUrl { get; set; }
    public string JungleBusBaseUrl { get; set; }
    public string JungleBusMempoolSubscriptionId { get; set; }
    public string JungleBusBlockSubscriptionId { get; set; }
    public string UpdatedBy { get; set; }

    public override string GetId() => DocumentId;

    public override IEnumerable<string> AllKeys()
    {
        foreach (var key in base.AllKeys())
            yield return key;

        yield return nameof(PrimaryRealtimeSource);
        yield return nameof(RawTxPrimaryProvider);
        yield return nameof(RestPrimaryProvider);
        yield return nameof(BitailsTransport);
        yield return nameof(BitailsApiKey);
        yield return nameof(BitailsBaseUrl);
        yield return nameof(BitailsWebsocketBaseUrl);
        yield return nameof(BitailsZmqTxUrl);
        yield return nameof(BitailsZmqBlockUrl);
        yield return nameof(WhatsonchainApiKey);
        yield return nameof(WhatsonchainBaseUrl);
        yield return nameof(JungleBusBaseUrl);
        yield return nameof(JungleBusMempoolSubscriptionId);
        yield return nameof(JungleBusBlockSubscriptionId);
        yield return nameof(UpdatedBy);
    }

    public override IEnumerable<string> UpdateableKeys()
    {
        foreach (var key in base.UpdateableKeys())
            yield return key;

        yield return nameof(PrimaryRealtimeSource);
        yield return nameof(RawTxPrimaryProvider);
        yield return nameof(RestPrimaryProvider);
        yield return nameof(BitailsTransport);
        yield return nameof(BitailsApiKey);
        yield return nameof(BitailsBaseUrl);
        yield return nameof(BitailsWebsocketBaseUrl);
        yield return nameof(BitailsZmqTxUrl);
        yield return nameof(BitailsZmqBlockUrl);
        yield return nameof(WhatsonchainApiKey);
        yield return nameof(WhatsonchainBaseUrl);
        yield return nameof(JungleBusBaseUrl);
        yield return nameof(JungleBusMempoolSubscriptionId);
        yield return nameof(JungleBusBlockSubscriptionId);
        yield return nameof(UpdatedBy);
    }

    public override IEnumerable<KeyValuePair<string, object>> ToEntries()
    {
        foreach (var entry in base.ToEntries())
            yield return entry;

        yield return new KeyValuePair<string, object>(nameof(PrimaryRealtimeSource), PrimaryRealtimeSource);
        yield return new KeyValuePair<string, object>(nameof(RawTxPrimaryProvider), RawTxPrimaryProvider);
        yield return new KeyValuePair<string, object>(nameof(RestPrimaryProvider), RestPrimaryProvider);
        yield return new KeyValuePair<string, object>(nameof(BitailsTransport), BitailsTransport);
        yield return new KeyValuePair<string, object>(nameof(BitailsApiKey), BitailsApiKey);
        yield return new KeyValuePair<string, object>(nameof(BitailsBaseUrl), BitailsBaseUrl);
        yield return new KeyValuePair<string, object>(nameof(BitailsWebsocketBaseUrl), BitailsWebsocketBaseUrl);
        yield return new KeyValuePair<string, object>(nameof(BitailsZmqTxUrl), BitailsZmqTxUrl);
        yield return new KeyValuePair<string, object>(nameof(BitailsZmqBlockUrl), BitailsZmqBlockUrl);
        yield return new KeyValuePair<string, object>(nameof(WhatsonchainApiKey), WhatsonchainApiKey);
        yield return new KeyValuePair<string, object>(nameof(WhatsonchainBaseUrl), WhatsonchainBaseUrl);
        yield return new KeyValuePair<string, object>(nameof(JungleBusBaseUrl), JungleBusBaseUrl);
        yield return new KeyValuePair<string, object>(nameof(JungleBusMempoolSubscriptionId), JungleBusMempoolSubscriptionId);
        yield return new KeyValuePair<string, object>(nameof(JungleBusBlockSubscriptionId), JungleBusBlockSubscriptionId);
        yield return new KeyValuePair<string, object>(nameof(UpdatedBy), UpdatedBy);
    }
}
