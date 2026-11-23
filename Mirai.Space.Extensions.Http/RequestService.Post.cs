using Mirai.Space.Extensions.Http.Abstractions;

namespace Mirai.Space.Extensions.Http;

internal sealed partial class RequestService
{
    public async Task<T?> PostAsync<T>(string url, CancellationToken ct = default)
    {
        using var request = CreateRequest(HttpMethod.Post, url);
        return await SendContentCoreAsync<T>(request, ct).ConfigureAwait(false);
    }

    public async Task<TResponse?> PostAsync<TResponse, TBody>(
        string url,
        TBody body,
        CancellationToken ct = default)
    {
        using var request = CreateRequestWithBody(HttpMethod.Post, url, body);
        return await SendContentCoreAsync<TResponse>(request, ct).ConfigureAwait(false);
    }

    public async Task PostAsync(string url, CancellationToken ct = default)
    {
        using var request = CreateRequest(HttpMethod.Post, url);
        await SendResponseCoreAsync(request, ct).ConfigureAwait(false);
    }

    public async Task PostAsync<TBody>(string url, TBody body, CancellationToken ct = default)
    {
        using var request = CreateRequestWithBody(HttpMethod.Post, url, body);
        await SendResponseCoreAsync(request, ct).ConfigureAwait(false);
    }

    public async Task<RequestResponse> PostResponseAsync(string url, CancellationToken ct = default)
    {
        using var request = CreateRequest(HttpMethod.Post, url);
        return await SendResponseCoreAsync(request, ct).ConfigureAwait(false);
    }

    public async Task<RequestResponse<TResponse>> PostResponseAsync<TResponse>(
        string url,
        CancellationToken ct = default)
    {
        using var request = CreateRequest(HttpMethod.Post, url);
        return await SendResponseCoreAsync<TResponse>(request, ct).ConfigureAwait(false);
    }

    public async Task<RequestResponse<TResponse>> PostResponseAsync<TResponse, TBody>(
        string url,
        TBody body,
        CancellationToken ct = default)
    {
        using var request = CreateRequestWithBody(HttpMethod.Post, url, body);
        return await SendResponseCoreAsync<TResponse>(request, ct).ConfigureAwait(false);
    }

    public async Task<RequestResponse> PostToStreamAsync(
        string url,
        Stream destination,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(destination);

        using var request = CreateRequest(HttpMethod.Post, url);
        return await SendStreamCoreAsync(request, destination, ct).ConfigureAwait(false);
    }

    public async Task<RequestResponse> PostToStreamAsync<TBody>(
        string url,
        TBody body,
        Stream destination,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(destination);

        using var request = CreateRequestWithBody(HttpMethod.Post, url, body);
        return await SendStreamCoreAsync(request, destination, ct).ConfigureAwait(false);
    }
}
