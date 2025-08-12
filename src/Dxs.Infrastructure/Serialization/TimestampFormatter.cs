using System;
using SpanJson;

namespace Dxs.Infrastructure.Serialization;

public class TimestampFormatter: ICustomJsonFormatter<DateTime>
{
    public static readonly TimestampFormatter Default = new ();

    public object Arguments { get; set; }

    public void Serialize(ref JsonWriter<byte> writer, DateTime value)
    {
        var ms = GetTotalMilliseconds(value);

        writer.WriteInt64(ms);
    }

    public DateTime Deserialize(ref JsonReader<byte> reader)
    {
        var milliseconds = reader.ReadInt64();
        return DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).UtcDateTime;
    }

    public void Serialize(ref JsonWriter<char> writer, DateTime value)
    {
        var ms = GetTotalMilliseconds(value);

        writer.WriteInt64(ms);
    }

    public DateTime Deserialize(ref JsonReader<char> reader)
    {
        var milliseconds = reader.ReadInt64();
        return DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).UtcDateTime;
    }

    private static long GetTotalMilliseconds(DateTime value)
        => new DateTimeOffset(value.ToUniversalTime()).ToUnixTimeMilliseconds();
}