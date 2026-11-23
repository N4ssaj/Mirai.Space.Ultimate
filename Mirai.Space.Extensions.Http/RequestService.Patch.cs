using Mirai.Space.Extensions.Http.Abstractions;

namespace Mirai.Space.Extensions.Http;

internal sealed partial class RequestService
{
    public async Task<TResponse?> PatchAsync<TResponse, TBody>(
        string url,
        TBody body,
        CancellationToken ct = default)
    {
        using var request = CreateRequestWithBody(HttpMethod.Patch, url, body);
        return await SendContentCoreAsync<TResponse>(request, ct).ConfigureAwait(false);
    }

    public async Task<TResponse?> PatchAsync<TResponse>(string url, CancellationToken ct = default)
    {
        using var request = CreateRequest(HttpMethod.Patch, url);
        return await SendContentCoreAsync<TResponse>(request, ct).ConfigureAwait(false);
    }

    public async Task PatchAsync<TBody>(string url, TBody body, CancellationToken ct = default)
    {
        using var request = CreateRequestWithBody(HttpMethod.Patch, url, body);
        await SendResponseCoreAsync(request, ct).ConfigureAwait(false);
    }

    public async Task PatchAsync(string url, CancellationToken ct = default)
    {
        using var request = CreateRequest(HttpMethod.Patch, url);
        await SendResponseCoreAsync(request, ct).ConfigureAwait(false);
    }

    public async Task<RequestResponse<TResponse>> PatchResponseAsync<TResponse, TBody>(
        string url,
        TBody body,
        CancellationToken ct = default)
    {
        using var request = CreateRequestWithBody(HttpMethod.Patch, url, body);
        return await SendResponseCoreAsync<TResponse>(request, ct).ConfigureAwait(false);
    }

    public async Task<RequestResponse<TResponse>> PatchResponseAsync<TResponse>(
        string url,
        CancellationToken ct = default)
    {
        using var request = CreateRequest(HttpMethod.Patch, url);
        return await SendResponseCoreAsync<TResponse>(request, ct).ConfigureAwait(false);
    }
}
