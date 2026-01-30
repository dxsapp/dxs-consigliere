using System.Globalization;

using SpanJson;
using SpanJson.Formatters;

namespace Dxs.Infrastructure.Serialization;

public class NullableDecimalAsStringFormatter : ICustomJsonFormatter<decimal?>
{
    public static readonly NullableDecimalAsStringFormatter Default = new();

    public object Arguments { get; set; }

    public void Serialize(ref JsonWriter<byte> writer, decimal? nullableValue)
    {
        if (nullableValue is { } value)
        {
            StringUtf8Formatter.Default.Serialize(ref writer, value.ToString(CultureInfo.InvariantCulture));
        }
    }

    public decimal? Deserialize(ref JsonReader<byte> reader)
    {
        var value = StringUtf8Formatter.Default.Deserialize(ref reader);

        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var result))
        {
            return result;
        }

        return null;
    }

    public void Serialize(ref JsonWriter<char> writer, decimal? nullableValue)
    {
        if (nullableValue is { } value)
        {
            StringUtf16Formatter.Default.Serialize(ref writer, value.ToString(CultureInfo.InvariantCulture));
        }
    }

    public decimal? Deserialize(ref JsonReader<char> reader)
    {
        var value = StringUtf16Formatter.Default.Deserialize(ref reader);

        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var result))
        {
            return result;
        }

        return null;
    }
}
