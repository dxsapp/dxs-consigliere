using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Dxs.Common.Extensions;

public static partial class StringExtensions
{
    /// <summary>
    /// Indicates whether a specified string is null, empty, or consists only of white-space characters
    /// </summary>
    public static bool IsNullOrWhiteSpace(this string value) => string.IsNullOrWhiteSpace(value);

    /// <summary>
    /// Indicates whether the specified string is null or an empty string ("").
    /// </summary>
    public static bool IsNullOrEmpty(this string value) => string.IsNullOrEmpty(value);

    /// <summary>
    /// Indicates whether the specified string is Not null and not an empty string ("").
    /// </summary>
    public static bool IsNotNullOrEmpty(this string value) => !string.IsNullOrEmpty(value);

    /// <summary>
    /// Indicates whether the specified string is Not null and not an empty string ("") and not consists only of white-space characters.
    /// </summary>
    public static bool IsNotNullOrEmptyOrWhiteSpace(this string value) => !string.IsNullOrEmpty(value) && !string.IsNullOrWhiteSpace(value);

    public static bool IsNullOrEmptyOrWhiteSpace(this string value) => string.IsNullOrEmpty(value) || string.IsNullOrWhiteSpace(value);

    public static byte[] ToUtf8Bytes(this string value) => Encoding.UTF8.GetBytes(value);

    public static string Filter(this string text, Func<char, bool> predicate)
    {
        var sb = new StringBuilder();

        foreach (var c in text.Where(predicate))
        {
            sb.Append(c);
        }

        return sb.ToString();
    }

    [GeneratedRegex(@"^([\w-.]+)@((\[[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.)|(([\w-]+\.)+))([a-zA-Z]{2,4}|[0-9]{1,3})(]?)$")]
    private static partial Regex EmailRegex();

    /// <summary>
    /// check if mail is valid
    /// <para>https://stackoverflow.com/a/13704625</para>
    /// </summary>
    /// <param name="email">email string</param>
    /// <returns>true if <paramref name="email"/> is in valid e-mail format.</returns>
    public static bool IsValidEmail(this string email) => EmailRegex().IsMatch(email);

    [GeneratedRegex(@"^\+[1-9][0-9]{7,14}$")]
    private static partial Regex PhoneNumberRegex();

    // only plus sign and numbers are allowed
    public static bool IsValidPhoneNumber(this string value) => value.IsNotNullOrEmpty() && PhoneNumberRegex().IsMatch(value);

    public static T ToEnum<T>(this string value, bool ignoreCase = true) => (T)Enum.Parse(typeof(T), value, ignoreCase);

    public static string OfMaxBytes(this string input, int maxBytes)
    {
        if (maxBytes == 0 || string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        var encoding = Encoding.UTF8;
        if (encoding.GetByteCount(input) <= maxBytes)
        {
            return input;
        }

        var sb = new StringBuilder();
        var bytes = 0;
        var enumerator = StringInfo.GetTextElementEnumerator(input);

        while (enumerator.MoveNext())
        {
            var textElement = enumerator.GetTextElement();
            bytes += encoding.GetByteCount(textElement);
            if (bytes <= maxBytes)
            {
                sb.Append(textElement);
            }
            else
            {
                break;
            }
        }

        return sb.ToString();
    }
}
