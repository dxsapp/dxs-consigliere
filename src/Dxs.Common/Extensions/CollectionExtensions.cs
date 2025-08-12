using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace Dxs.Common.Extensions;

public static class CollectionExtensions
{
    public static ReadOnlyDictionary<TKey, TValue> ToReadOnly<TKey, TValue>(this IDictionary<TKey, TValue> dictionary) => new(dictionary);

    public static ReadOnlyCollection<T> ToReadOnly<T>(this IList<T> list) => new(list);

    public static TValue GetOrAdd<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key, TValue value)
    {
        if (dictionary.TryGetValue(key, out var existing))
            return existing;

        dictionary.Add(key, value);
        return value;
    }
        
    public static void AddOrUpdate<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key,  Action<TValue> update) where TValue: new()
    {
        dictionary.TryAdd(key, new TValue());
        update(dictionary[key]);
    }
        
    public static void AddOrUpdate<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key,  TValue value) where TValue: new()
    {
        dictionary.TryAdd(key, new TValue());
        dictionary[key] = value;
    }

    public static TValue GetOrNull<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key) where TValue: class =>
        dictionary.TryGetValue(key, out var existing) ? existing : null;

    public static TValue GetOrNull<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> dictionary, TKey key) where TValue: class =>
        dictionary.TryGetValue(key, out var existing) ? existing : null;

    public static IEnumerable<TValue> Values<TKey, TValue>(this ILookup<TKey, TValue> lookup) => lookup.SelectMany(g => g);

    public static Dictionary<TKey, TValue> ToDictionaryWithFirstValue<TSource, TKey, TValue>(this IEnumerable<TSource> source,
        Func<TSource, TKey> keySelector, Func<TSource, TValue> valueSelector)
    {
        return source
            .GroupBy(keySelector)
            .ToDictionary(g => g.Key, g => g.Select(valueSelector).First());
    }

    public static Dictionary<TKey, TSource> ToDictionaryWithFirstValue<TSource, TKey>(this IEnumerable<TSource> source,
        Func<TSource, TKey> keySelector)
    {
        return source.ToDictionaryWithFirstValue(keySelector, e => e);
    }

    public static Dictionary<TKey, TValue> ToDictionaryWithFirstValue<TKey, TValue>(this IEnumerable<KeyValuePair<TKey, TValue>> source) => source
        .ToDictionaryWithFirstValue(pair => pair.Key, pair => pair.Value);

    public static HashSet<TKey> ToHashSet<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector) =>
        source.Select(keySelector).ToHashSet();

    public static NameValueCollection ToNameValueCollection(this IEnumerable<KeyValuePair<string, string>> source)
    {
        var result = new NameValueCollection();
        foreach (var (key, value) in source)
            result.Add(key, value);

        return result;
    }

    public static IEnumerable<KeyValuePair<string, string[]>> AsEnumerable(this NameValueCollection source) => source
        .Cast<string>()
        .Select(key => KeyValuePair.Create(key, source.GetValues(key)));

    public static IEnumerable<KeyValuePair<string, string>> AsKeyValuePairs(this NameValueCollection source) => source
        .Cast<string>()
        .SelectMany(source.GetValues, KeyValuePair.Create);

    public static NameValueCollection ToNameValueCollection(this IEnumerable<KeyValuePair<string, IEnumerable<string>>> source) => source
        .SelectMany(pair => pair.Value, (pair, value) => KeyValuePair.Create(pair.Key, value))
        .ToNameValueCollection();

    public static (T[] toAdd, T[] toRemove) ElementsDifference<T>(this HashSet<T> source, HashSet<T> destination)
    {
        return (
            toAdd: destination.Where(e => !source.Contains(e)).ToArray(),
            toRemove: source.Where(e => !destination.Contains(e)).ToArray()
        );
    }

    public static T GetValueOrDefault<T>(this HashSet<T> source, T equalValue) => source.TryGetValue(equalValue, out var value)
        ? value
        : default;

    public static void AddRange<T>(this ICollection<T> source, IEnumerable<T> values)
    {
        if (source is List<T> list)
        {
            list.AddRange(values);
        }
        else
        {
            foreach (var value in values)
                source.Add(value);
        }
    }

    public static void RemoveRange<T>(this ISet<T> source, IEnumerable<T> values)
    {
        foreach (var value in values)
            source.Remove(value);
    }

    public static Dictionary<TKey, TValue> With<TKey, TValue>(this Dictionary<TKey, TValue> source, TKey key, TValue value)
    {
        source[key] = value;
        return source;
    }

    public static IList<T> AsIList<T>(this IEnumerable<T> source) => source as IList<T> ?? source.ToList();

    /// <summary>
    /// Enumerates <paramref name="source"/> to a new <see cref="List{T}"/> starting from <paramref name="initialCapacity"/> size.
    /// This allows to avoid or minimize list resizing if number of elements is known fully or approximately.
    /// </summary>
    public static List<T> ToList<T>(this IEnumerable<T> source, int initialCapacity)
    {
        var list = new List<T>(initialCapacity);
        list.AddRange(source);
        return list;
    }

    /// <summary>
    /// Enumerates <paramref name="source"/> to a new <see cref="T:T[]"/> of <paramref name="capacity"/> size.
    /// This allows to avoid array resizing if number of elements is fully known.
    /// </summary>
    /// <exception cref="ArgumentException">Source count is different from <paramref name="capacity"/></exception>
    public static T[] ToArray<T>(this IEnumerable<T> source, int capacity)
    {
        var (array, index) = (new T[capacity], 0);
        foreach (var elem in source)
        {
            if (index >= array.Length)
                throw new ArgumentException($"Source has more than {capacity} elements.");

            array[index++] = elem;
        }

        if (index < array.Length)
            throw new ArgumentException($"Source has less than {capacity} elements.");

        return array;
    }

    public static bool TryRemove<TKey, TValue>(this ICollection<KeyValuePair<TKey, TValue>> source, TKey key, TValue value) =>
        source.Remove(new KeyValuePair<TKey, TValue>(key, value));

    public static T[] ToArrayOrNull<T>(this IEnumerable<T> source, out Exception exception)
    {
        exception = null;
        try
        {
            return source.ToArray();
        }
        catch (Exception ex)
        {
            exception = ex;
            return null;
        }
    }

    public static bool TryGet<T>(this IList<T> source, int index, out T value)
    {
        if (index >= 0 && source.Count > index)
        {
            value = source[index];
            return true;
        }

        value = default;
        return false;
    }

    public static Dictionary<TKey, TValue> EnsureKeys<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, IEnumerable<TKey> keys,
        TValue defaultValue = default)
    {
        foreach (var key in keys)
            dictionary.TryAdd(key, defaultValue);

        return dictionary;
    }
}