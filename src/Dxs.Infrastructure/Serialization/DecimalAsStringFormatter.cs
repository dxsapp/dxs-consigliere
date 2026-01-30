using System;
using System.Globalization;

using SpanJson;
using SpanJson.Formatters;

namespace Dxs.Infrastructure.Serialization;

public sealed class DecimalAsStringFormatter : ICustomJsonFormatter<decimal>
{
    public static readonly DecimalAsStringFormatter Default = new();

    public object Arguments { get; set; }

    public void Serialize(ref JsonWriter<byte> writer, decimal value)
    {
        StringUtf8Formatter.Default.Serialize(ref writer, value.ToString(CultureInfo.InvariantCulture));
    }

    public decimal Deserialize(ref JsonReader<byte> reader)
    {
        var value = StringUtf8Formatter.Default.Deserialize(ref reader);

        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal result))
        {
            return result;
        }

        throw new InvalidOperationException("Invalid value.");
    }

    public void Serialize(ref JsonWriter<char> writer, decimal value)
    {
        StringUtf16Formatter.Default.Serialize(ref writer, value.ToString(CultureInfo.InvariantCulture));
    }

    public decimal Deserialize(ref JsonReader<char> reader)
    {
        var value = StringUtf16Formatter.Default.Deserialize(ref reader);

        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal result))
        {
            return result;
        }

        throw new InvalidOperationException("Invalid value.");
    }
}
