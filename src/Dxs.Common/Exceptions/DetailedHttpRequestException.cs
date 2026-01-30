using System.Collections.Specialized;

using Dxs.Common.Extensions;

namespace Dxs.Common.Exceptions;

public class DetailedHttpRequestException(
    HttpResponseMessage response,
    string responseContent
) : HttpRequestException(
    $"Response code validation failed {response.RequestMessage?.Method} {response.RequestMessage?.RequestUri}\n" +
    $"Response: {response.StatusCode} {responseContent}",
    inner: null,
    statusCode: response.StatusCode
)
{
    public NameValueCollection RequestHeaders { get; } = response.RequestMessage?.Headers?.ToNameValueCollection() ?? new();

    public static async Task<DetailedHttpRequestException> FromResponseAsync(HttpResponseMessage response)
    {
        string responseContent;

        try
        {
            responseContent = await response.Content.ReadAsStringAsync();
        }
        catch
        {
            responseContent = null;
        }

        throw new DetailedHttpRequestException(response, responseContent);
    }
}
