using System;
using System.Globalization;

using SpanJson;
using SpanJson.Formatters;

namespace Dxs.Infrastructure.Serialization;

public sealed class DoubleAsStringFormatter : ICustomJsonFormatter<double>
{
    public static readonly DoubleAsStringFormatter Default = new();

    public object Arguments { get; set; }

    public void Serialize(ref JsonWriter<byte> writer, double value)
    {
        StringUtf8Formatter.Default.Serialize(ref writer, value.ToString(CultureInfo.InvariantCulture));
    }

    public double Deserialize(ref JsonReader<byte> reader)
    {
        var value = StringUtf8Formatter.Default.Deserialize(ref reader);
        if (double.TryParse(value, out double result))
        {
            return result;
        }

        throw new InvalidOperationException("Invalid value.");
    }

    public void Serialize(ref JsonWriter<char> writer, double value)
    {
        StringUtf16Formatter.Default.Serialize(ref writer, value.ToString(CultureInfo.InvariantCulture));
    }

    public double Deserialize(ref JsonReader<char> reader)
    {
        var value = StringUtf16Formatter.Default.Deserialize(ref reader);
        if (double.TryParse(value, out double result))
        {
            return result;
        }

        throw new InvalidOperationException("Invalid value.");
    }
}
