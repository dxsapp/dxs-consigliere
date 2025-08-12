using System;
using System.Globalization;
using SpanJson;
using SpanJson.Formatters;

namespace Dxs.Infrastructure.Serialization;

public class LongAsStringFormatter: ICustomJsonFormatter<long>
{
    public static readonly LongAsStringFormatter Default = new();

    public object Arguments { get; set; }

    public void Serialize(ref JsonWriter<byte> writer, long value)
    {
        StringUtf8Formatter.Default.Serialize(ref writer, value.ToString(CultureInfo.InvariantCulture));
    }

    public long Deserialize(ref JsonReader<byte> reader)
    {
        var value = StringUtf8Formatter.Default.Deserialize(ref reader);
        if (long.TryParse(value, out long result))
        {
            return result;
        }

        throw new InvalidOperationException("Invalid value.");
    }

    public void Serialize(ref JsonWriter<char> writer, long value)
    {
        StringUtf16Formatter.Default.Serialize(ref writer, value.ToString(CultureInfo.InvariantCulture));
    }

    public long Deserialize(ref JsonReader<char> reader)
    {
        var value = StringUtf16Formatter.Default.Deserialize(ref reader);
        if (long.TryParse(value, out long result))
        {
            return result;
        }

        throw new InvalidOperationException("Invalid value.");
    }
}