using System.Collections;
using System.Collections.Specialized;
using System.Web;

namespace Dxs.Common.Utils;

public class QueryBuilder: IEnumerable
{
    private readonly NameValueCollection _values;

    public QueryBuilder(string query = null) => _values = HttpUtility.ParseQueryString(query ?? "");

    public QueryBuilder(string key, string value) => _values = new NameValueCollection { { key, value } };

    public QueryBuilder(NameValueCollection values): this() => Add(values);

    public QueryBuilder Add(string name, string value)
    {
        if (value != null)
            _values.Add(name, value);

        return this;
    }

    public QueryBuilder Add(string name, object value) => Add(name, value == null ? null : $"{value}");

    public QueryBuilder Add(string name, string[] values)
    {
        if (values != null)
        {
            foreach (var value in values)
            {
                Add(name, value);
            }
        }

        return this;
    }

    public QueryBuilder Add(NameValueCollection values) => Add(values.Keys.Cast<string>().Select(key => KeyValuePair.Create(key, (object)values[key])));

    public QueryBuilder Add(object values)
    {
        if (values != null)
            Add(PropertyHelper.ObjectToDictionary(values));

        return this;
    }

    public QueryBuilder Add(IEnumerable<KeyValuePair<string, object>> values)
    {
        foreach (var pair in values)
            Add(pair);

        return this;
    }

    public IEnumerator GetEnumerator() => _values.GetEnumerator();

    public override string ToString()
    {
        var pairs = new List<string>();
        var keys = _values.Keys.Cast<string>();

        foreach (var key in keys)
        {
            var values = _values.GetValues(key) ?? Array.Empty<string>();

            foreach (var value in values.Where(x => x != null))
            {
                pairs.Add($"{key}={HttpUtility.UrlEncode(value)}");
            }
        }

        return string.Join('&', pairs);
    }

    public static implicit operator string(QueryBuilder builder) => builder.ToString();

    private QueryBuilder Add(KeyValuePair<string, object> pair) => Add(pair.Key, pair.Value);
}