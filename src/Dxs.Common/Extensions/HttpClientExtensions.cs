#nullable enable
using System.ComponentModel.DataAnnotations;
using Dxs.Common.Content;
using Dxs.Common.Exceptions;
using Dxs.Common.Utils;
using Newtonsoft.Json;

namespace Dxs.Common.Extensions;

// Supports JSON only
public static class HttpClientExtensions
{
    private static readonly JsonSerializerSettings JsonSettings = new();

    public static async Task<T> GetAsync<T>(
        this HttpClient client,
        string requestUri,
        HttpCompletionOption completionOption,
        CancellationToken cancellationToken = default
    )
    {
        var response = await client.GetAsync(requestUri, completionOption, cancellationToken);
        return await response.Content.ReadAsAsync<T>(cancellationToken);
    }

    public static Task<T> GetAsync<T>(
        this HttpClient client,
        string requestUri,
        CancellationToken cancellationToken = default
    ) =>
        client.GetAsync<T>(requestUri, HttpCompletionOption.ResponseContentRead, cancellationToken);

    public static async Task EnsureSuccessStatusCodeAsync(this HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
            throw await DetailedHttpRequestException.FromResponseAsync(response);
    }

    public static async Task<T> ReadContentOrThrowAsync<T>(
        this HttpResponseMessage response,
        bool validateModel = true,
        CancellationToken cancellationToken = default
    )
    {
        await response.EnsureSuccessStatusCodeAsync();
        var contentString = await response.Content.ReadAsStringAsync(cancellationToken);

        T content;
        try
        {
            content = JsonConvert.DeserializeObject<T>(contentString, JsonSettings)!;
            if (validateModel)
            {
                Validator.ValidateObject(content, new ValidationContext(content));
            }
        }
        catch (JsonSerializationException exception)
        {
            throw BuildException(response, contentString, exception);
        }
        catch (ValidationException exception)
        {
            throw BuildException(response, contentString, exception);
        }

        return content;
    }

    private static async Task<HttpResponseMessage> SendOrThrowAsync(
        this HttpClient client,
        HttpRequestMessage request,
        CancellationToken token = default
    )
    {
        var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        await response.EnsureSuccessStatusCodeAsync();
        return response;
    }

    public static Task<HttpResponseMessage> SendOrThrowAsync(
        this HttpClient client,
        HttpMethod method,
        string url,
        object? value = null,
        object? headers = null,
        CancellationToken token = default
    )
    {
        var headersMap = headers is IEnumerable<KeyValuePair<string, string>> headerPairs
            ? new Dictionary<string, string>(headerPairs, StringComparer.OrdinalIgnoreCase)
            : PropertyHelper
                .ObjectToDictionary(headers)
                .ToDictionary(p => p.Key, p => $"{p.Value}");

        var message = new HttpRequestMessage(method, url)
            {
                // remove CharSet as some servers don't understand it
                Content = value == null
                    ? null
                    : value is string json
                        ? new JsonContent(json, noCharSet: true).AddHeaders(headersMap)
                        : new JsonContent(value, noCharSet: true).AddHeaders(headersMap)


            }
            .AddHeaders(headersMap);

        return client.SendOrThrowAsync(message, token);
    }

    public static async Task<T> SendOrThrowAsync<T>(
        this HttpClient client,
        HttpMethod method,
        string url,
        object? value = null,
        object? headers = null,
        bool validateModel = true,
        CancellationToken token = default
    )
    {
        var response = await client.SendOrThrowAsync(method, url, value, headers, token);

        return await response.ReadContentOrThrowAsync<T>(validateModel, token);
    }

    public static async Task<T> SendOrThrowAsync<T>(
        this HttpClient client,
        HttpRequestMessage request,
        bool validateModel,
        CancellationToken token = default
    )
    {
        var response = await client.SendOrThrowAsync(request, token);

        return await response.ReadContentOrThrowAsync<T>(validateModel, token);
    }

    public static Task<T> SendOrThrowAsync<T>(
        this HttpClient client,
        HttpRequestMessage request,
        CancellationToken token = default
    ) =>
        client.SendOrThrowAsync<T>(request, validateModel: true, token);

    public static Task<T> SendOrThrowAsync<T>(
        this HttpClient client,
        HttpMethod method,
        string url,
        object? headers,
        bool validateModel = true,
        CancellationToken token = default
    ) =>
        client.SendOrThrowAsync<T>(method, url, value: null, headers, validateModel, token);

    public static Task<T> PostOrThrowAsync<T>(
        this HttpClient client,
        string requestUri,
        object? value,
        object? headers,
        bool validateModel,
        CancellationToken token = default
    ) =>
        client.SendOrThrowAsync<T>(HttpMethod.Post, requestUri, value, headers, validateModel, token);

    public static Task<T> PostOrThrowAsync<T>(
        this HttpClient client,
        string requestUri,
        object? value,
        object? headers,
        CancellationToken token = default
    ) =>
        client.PostOrThrowAsync<T>(requestUri, value, headers, validateModel: true, token);

    public static Task<T> PostOrThrowAsync<T>(
        this HttpClient client,
        string requestUri,
        object? value,
        bool validateModel,
        CancellationToken token = default
    ) =>
        client.PostOrThrowAsync<T>(requestUri, value, headers: null, validateModel, token);

    public static Task<T> PostOrThrowAsync<T>(
        this HttpClient client,
        string requestUri,
        object? value,
        CancellationToken token = default
    ) =>
        client.PostOrThrowAsync<T>(requestUri, value, headers: null, validateModel: true, token);

    public static Task<T> GetOrThrowAsync<T>(
        this HttpClient client,
        string requestUri,
        object? headers,
        bool validateModel,
        CancellationToken token = default
    ) =>
        client.SendOrThrowAsync<T>(HttpMethod.Get, requestUri, null, headers, validateModel, token);

    public static Task<T> GetOrThrowAsync<T>(
        this HttpClient client,
        string requestUri,
        bool validateModel,
        CancellationToken token = default
    ) =>
        client.GetOrThrowAsync<T>(requestUri, headers: null, validateModel, token: token);

    public static Task<T> GetOrThrowAsync<T>(
        this HttpClient client,
        string requestUri,
        CancellationToken token = default
    ) =>
        client.GetOrThrowAsync<T>(requestUri, headers: null, validateModel: true, token: token);

    public static HttpRequestMessage AddHeaders(
        this HttpRequestMessage request,
        Dictionary<string, string>? headers
    )
    {
        if (headers == null)
            return request;

        foreach (var (key, value) in headers.ToList())
        {
            if (request.Headers.TryAddWithoutValidation(key, value))
            {
                headers.Remove(key);
            }
        }

        return request;
    }

    public static HttpContent AddHeaders(this HttpContent content, Dictionary<string, string>? headers)
    {
        if (headers == null)
            return content;

        foreach (var (key, value) in headers.ToList())
        {
            if (content.Headers.TryAddWithoutValidation(key, value))
            {
                headers.Remove(key);
            }
        }

        return content;
    }

    public static HttpRequestException BuildException(
        HttpResponseMessage response,
        string contentString,
        Exception exception
    )
    {
        var request = response.RequestMessage;

        return new HttpRequestException(
            $"Response content deserialization failed {request?.Method} {request?.RequestUri}\n" +
            $"Response: {response.StatusCode} {contentString}\n",
            exception
        );
    }

    private static async Task<T> ReadAsAsync<T>(this HttpContent content, CancellationToken token)
        => JsonConvert.DeserializeObject<T>(await content.ReadAsStringAsync(token));
}