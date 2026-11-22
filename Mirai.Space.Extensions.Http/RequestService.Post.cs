using Mirai.Space.Extensions.Http.Abstractions;

namespace Mirai.Space.Extensions.Http;

internal sealed partial class RequestService
{
    public async Task<T?> PostAsync<T>(string url, CancellationToken ct = default) =>
        (await SendCoreAsync<T>(ApiRequest.Post(url), ct).ConfigureAwait(false)).Content;

    public async Task<TResponse?> PostAsync<TResponse, TBody>(
        string url,
        TBody body,
        CancellationToken ct = default) =>
        (await SendCoreAsync<TResponse>(ApiRequest.Post(url, CreateContent(body)), ct).ConfigureAwait(false)).Content;

    public Task PostAsync(string url, CancellationToken ct = default) =>
        SendCoreAsync(ApiRequest.Post(url), ct);

    public Task PostAsync<TBody>(string url, TBody body, CancellationToken ct = default) =>
        SendCoreAsync(ApiRequest.Post(url, CreateContent(body)), ct);

    public Task<ApiResponse<TResponse>> PostResponseAsync<TResponse, TBody>(
        string url,
        TBody body,
        CancellationToken ct = default) =>
        SendCoreAsync<TResponse>(ApiRequest.Post(url, CreateContent(body)), ct);

    public Task<ApiResponse> PostStreamAsync(
        string url,
        Stream destination,
        CancellationToken ct = default) =>
        SendForStreamCoreAsync(ApiRequest.Post(url), destination, disposeRequest: true, ct);

    public Task<ApiResponse> PostStreamAsync<TBody>(
        string url,
        TBody body,
        Stream destination,
        CancellationToken ct = default) =>
        SendForStreamCoreAsync(ApiRequest.Post(url, CreateContent(body)), destination, disposeRequest: true, ct);
}
