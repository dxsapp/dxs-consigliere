using System.Text;

using Newtonsoft.Json;

namespace Dxs.Common.Content;

public class JsonContent : StringContent
{
    public JsonContent(
        string value,
        bool noCharSet = false
    ) : base(value, Encoding.UTF8, "application/json")
    {
        if (noCharSet && Headers.ContentType is { } contentType)
        {
            contentType.CharSet = null;
        }
    }

    public JsonContent(
        object value,
        JsonSerializerSettings settings = null,
        bool noCharSet = false
    ) : this(JsonConvert.SerializeObject(value, settings), noCharSet) { }
}
