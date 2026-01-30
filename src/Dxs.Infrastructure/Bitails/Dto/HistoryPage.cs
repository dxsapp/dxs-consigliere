using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Dxs.Infrastructure.Bitails.Dto;

[JsonObject]
public class HistoryPage : IEnumerable<HistoryEntry>
{
    [JsonProperty("pgkey")]
    public string Pgkey { get; private init; }

    [JsonProperty("history", Required = Required.Always)]
    private HistoryEntry[] History { get; init; }

    public int Count => History.Length;

    public IEnumerator<HistoryEntry> GetEnumerator() => History.AsEnumerable().GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => History.GetEnumerator();
}

public class HistoryEntry
{
    [JsonProperty("txid", Required = Required.Always)]
    public string TxId { get; private init; }

    [JsonProperty("time", Required = Required.Always)]
    [JsonConverter(typeof(UnixDateTimeConverter))]
    public DateTime Timestamp { get; private init; }
}
