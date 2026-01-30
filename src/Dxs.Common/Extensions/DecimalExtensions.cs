using System;
using System.Globalization;

namespace TrustMargin.Common.Extensions;

public static class DecimalExtensions
{
    public const int SafeDecimals = 15;

    private static readonly CultureInfo UsCulture = new CultureInfo("en-US");

    public static double AsDouble(this decimal value) => decimal.ToDouble(value);

    public static decimal ParseUS(string stringNumber) => decimal.Parse(stringNumber, NumberStyles.Number, UsCulture);

    public static decimal SafeRound(this decimal value) => Math.Round(value, SafeDecimals);
}
