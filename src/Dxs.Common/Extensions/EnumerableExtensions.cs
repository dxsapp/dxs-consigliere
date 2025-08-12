using System.Collections;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Dxs.Common.Extensions;

public static class EnumerableExtensions
{
    public static string ToString(this IEnumerable<string> source, string separator) => string.Join(separator, source);
        
    public static IEnumerable<T> Order<T>(this IEnumerable<T> source) =>
        source.OrderBy(_ => _);

    public static IEnumerable<T> OrderDescending<T>(this IEnumerable<T> source) =>
        source.OrderByDescending(_ => _);

    public static bool InCollection<T>(this T element, IEnumerable<T> source) => source.Contains(element);
    public static bool InCollection<T>(this T element, params T[] source) => element.InCollection(source.AsEnumerable());

    public static DateTime Average(this IEnumerable<DateTime> source)
    {
        var (ticks, count) = ((BigInteger)0, 0);
        foreach (var dateTime in source)
        {
            ticks += dateTime.Ticks;
            count++;
        }

        return new DateTime((long)(ticks / count));
    }

    public static DateTime Average<T>(this IEnumerable<T> source, Func<T, DateTime> selector) =>
        source.Select(selector).Average();

    public static IEnumerable<TCast> CastOfType<TCast>(this IEnumerable source) =>
        source.Cast<object>().OfType<TCast>();

    public static bool Any(this IEnumerable source) => Enumerable.Any(source.Cast<object>());

    public static IEnumerable<TSelected> SelectIterate<T, TSelected>(this IEnumerable<T> list, Func<T, int, TSelected> func)
    {
        var enumerable = list as T[] ?? list.ToArray();
        for (var i = 0; i < enumerable.Length; i++)
        {
            var x = enumerable[i];
            yield return func(x, i);
        }
    }

    public static IEnumerable<TKey> Keys<TKey, TValue>(this IEnumerable<KeyValuePair<TKey, TValue>> source) =>
        source.Select(e => e.Key);

    public static IEnumerable<TValue> Values<TKey, TValue>(this IEnumerable<KeyValuePair<TKey, TValue>> source) =>
        source.Select(e => e.Value);

    public static IEnumerable<T> UnionOne<T>(this IEnumerable<T> source, T element) => source.Union(new[] { element });

    public static void Deconstruct<T>(this IEnumerable<T> source, out T value1, out T value2)
    {
        using var enumerator = source.GetEnumerator();

        if (!enumerator.MoveNext()) throw new IndexOutOfRangeException("Source is too short to deconstruct.");
        value1 = enumerator.Current;

        if (!enumerator.MoveNext()) throw new IndexOutOfRangeException("Source is too short to deconstruct.");
        value2 = enumerator.Current;
    }

    public static IEnumerable<T> Prepend<T>(this IEnumerable<T> source, T element) => Enumerable.Repeat(element, 1).Union(source);

    public static IEnumerable<(T element, int index)> Enumerate<T>(this IEnumerable<T> source) =>
        source.Select((element, index) => (element, index));

    public static IEnumerable<T> ThrowIfNull<T>(this IEnumerable<T> source, [CallerArgumentExpression("source")] string sourceExpression = default) =>
        source ?? throw new ArgumentNullException(paramName: sourceExpression);

    public static ulong Sum<TSource>(this IEnumerable<TSource> source, Func<TSource, ulong> selector)
    {
        if (source == null)
            throw new ArgumentNullException(paramName: nameof(source));

        if (selector == null)
            throw new ArgumentNullException(paramName: nameof(selector));

        return source.Aggregate<TSource, ulong>(0, (current, item) => current + selector(item));
    }
}