using System;
using System.Globalization;
using SpanJson;
using SpanJson.Formatters;

namespace Dxs.Infrastructure.Serialization;

public class TimestampAsStringFormatter: ICustomJsonFormatter<DateTime>
{
    public static readonly TimestampAsStringFormatter Default = new ();

    public object Arguments { get; set; }

    public void Serialize(ref JsonWriter<byte> writer, DateTime value)
    {
        var ms = GetTotalMilliseconds(value);

        StringUtf8Formatter.Default.Serialize(ref writer, ms.ToString(CultureInfo.InvariantCulture));
    }

    public DateTime Deserialize(ref JsonReader<byte> reader)
    {
        var value = StringUtf8Formatter.Default.Deserialize(ref reader);
        return long.TryParse(value, out var milliseconds)
            ? DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).UtcDateTime
            : throw new InvalidOperationException("Invalid value.");

    }

    public void Serialize(ref JsonWriter<char> writer, DateTime value)
    {
        var ms = GetTotalMilliseconds(value);

        StringUtf16Formatter.Default.Serialize(ref writer, ms.ToString(CultureInfo.InvariantCulture));
    }

    public DateTime Deserialize(ref JsonReader<char> reader)
    {
        var value = StringUtf16Formatter.Default.Deserialize(ref reader);
        return long.TryParse(value, out var milliseconds)
            ? DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).UtcDateTime
            : throw new InvalidOperationException("Invalid value.");

    }

    private static long GetTotalMilliseconds(DateTime value)
        => new DateTimeOffset(value.ToUniversalTime()).ToUnixTimeMilliseconds();
}