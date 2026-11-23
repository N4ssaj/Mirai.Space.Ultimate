using Mirai.Space.Extensions.Http.Abstractions;

namespace Mirai.Space.Extensions.Http;

internal sealed partial class RequestService
{
    public async Task DeleteAsync(string url, CancellationToken ct = default)
    {
        using var request = CreateRequest(HttpMethod.Delete, url);
        await SendResponseCoreAsync(request, ct).ConfigureAwait(false);
    }

    public async Task<T?> DeleteAsync<T>(string url, CancellationToken ct = default)
    {
        using var request = CreateRequest(HttpMethod.Delete, url);
        return await SendContentCoreAsync<T>(request, ct).ConfigureAwait(false);
    }

    public async Task DeleteAsync<TBody>(string url, TBody body, CancellationToken ct = default)
    {
        using var request = CreateRequestWithBody(HttpMethod.Delete, url, body);
        await SendResponseCoreAsync(request, ct).ConfigureAwait(false);
    }

    public async Task<TResponse?> DeleteAsync<TResponse, TBody>(
        string url,
        TBody body,
        CancellationToken ct = default)
    {
        using var request = CreateRequestWithBody(HttpMethod.Delete, url, body);
        return await SendContentCoreAsync<TResponse>(request, ct).ConfigureAwait(false);
    }

    public async Task<RequestResponse<T>> DeleteResponseAsync<T>(string url, CancellationToken ct = default)
    {
        using var request = CreateRequest(HttpMethod.Delete, url);
        return await SendResponseCoreAsync<T>(request, ct).ConfigureAwait(false);
    }

    public async Task<RequestResponse<TResponse>> DeleteResponseAsync<TResponse, TBody>(
        string url,
        TBody body,
        CancellationToken ct = default)
    {
        using var request = CreateRequestWithBody(HttpMethod.Delete, url, body);
        return await SendResponseCoreAsync<TResponse>(request, ct).ConfigureAwait(false);
    }
}
