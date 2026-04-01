using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using Dxs.Common.Exceptions;
using Dxs.Common.Extensions;
using Dxs.Infrastructure.Common;
using Dxs.Infrastructure.JungleBus.Dto;

namespace Dxs.Infrastructure.JungleBus;

public sealed class JungleBusRawTransactionClient(
    HttpClient httpClient,
    IExternalChainProviderSettingsAccessor providerSettingsAccessor
) : IJungleBusRawTransactionClient
{
    public async Task<byte[]> GetTransactionRawOrNullAsync(string txId, CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Get,
            await BuildUrlAsync($"v1/transaction/get/{txId}", cancellationToken));

        ApplyHeaders(request, await BuildHeadersAsync(cancellationToken));
        var response = await httpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        if (!response.IsSuccessStatusCode)
            throw await DetailedHttpRequestException.FromResponseAsync(response);

        var dto = await response.ReadContentOrThrowAsync<PubTransactionDto>(cancellationToken: cancellationToken);
        return string.IsNullOrWhiteSpace(dto.TransactionBase64)
            ? null
            : Convert.FromBase64String(dto.TransactionBase64);
    }

    private async Task<string> BuildUrlAsync(string relativePath, CancellationToken cancellationToken)
    {
        var settings = await providerSettingsAccessor.GetJungleBusAsync(cancellationToken);
        var baseUrl = string.IsNullOrWhiteSpace(settings.BaseUrl)
            ? "https://junglebus.gorillapool.io"
            : settings.BaseUrl.Trim().TrimEnd('/');
        return $"{baseUrl}/{relativePath.TrimStart('/')}";
    }

    private async Task<Dictionary<string, string>> BuildHeadersAsync(CancellationToken cancellationToken)
    {
        var settings = await providerSettingsAccessor.GetJungleBusAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
            return null;

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Authorization"] = settings.ApiKey
        };
    }

    private static void ApplyHeaders(HttpRequestMessage request, IReadOnlyDictionary<string, string> headers)
    {
        if (headers is null)
            return;

        foreach (var header in headers)
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
    }
}
