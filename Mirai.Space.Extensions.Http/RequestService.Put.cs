using Mirai.Space.Extensions.Http.Abstractions;

namespace Mirai.Space.Extensions.Http;

internal sealed partial class RequestService
{
    public async Task<TResponse?> PutAsync<TResponse, TBody>(
        string url,
        TBody body,
        CancellationToken ct = default)
    {
        using var request = CreateRequestWithBody(HttpMethod.Put, url, body);
        return await SendContentCoreAsync<TResponse>(request, ct).ConfigureAwait(false);
    }

    public async Task<TResponse?> PutAsync<TResponse>(string url, CancellationToken ct = default)
    {
        using var request = CreateRequest(HttpMethod.Put, url);
        return await SendContentCoreAsync<TResponse>(request, ct).ConfigureAwait(false);
    }

    public async Task PutAsync<TBody>(string url, TBody body, CancellationToken ct = default)
    {
        using var request = CreateRequestWithBody(HttpMethod.Put, url, body);
        await SendResponseCoreAsync(request, ct).ConfigureAwait(false);
    }

    public async Task PutAsync(string url, CancellationToken ct = default)
    {
        using var request = CreateRequest(HttpMethod.Put, url);
        await SendResponseCoreAsync(request, ct).ConfigureAwait(false);
    }

    public async Task<RequestResponse<TResponse>> PutResponseAsync<TResponse, TBody>(
        string url,
        TBody body,
        CancellationToken ct = default)
    {
        using var request = CreateRequestWithBody(HttpMethod.Put, url, body);
        return await SendResponseCoreAsync<TResponse>(request, ct).ConfigureAwait(false);
    }

    public async Task<RequestResponse<TResponse>> PutResponseAsync<TResponse>(
        string url,
        CancellationToken ct = default)
    {
        using var request = CreateRequest(HttpMethod.Put, url);
        return await SendResponseCoreAsync<TResponse>(request, ct).ConfigureAwait(false);
    }
}
