using Mirai.Space.Extensions.Http.Abstractions;

namespace Mirai.Space.Extensions.Http;

internal sealed partial class RequestService
{
    public async Task<TResponse?> PutAsync<TResponse, TBody>(
        string url,
        TBody body,
        CancellationToken ct = default) =>
        (await SendCoreAsync<TResponse>(ApiRequest.Put(url, CreateContent(body)), ct).ConfigureAwait(false)).Content;

    public Task PutAsync<TBody>(string url, TBody body, CancellationToken ct = default) =>
        SendCoreAsync(ApiRequest.Put(url, CreateContent(body)), ct);

    public async Task<TResponse?> PatchAsync<TResponse, TBody>(
        string url,
        TBody body,
        CancellationToken ct = default) =>
        (await SendCoreAsync<TResponse>(ApiRequest.Patch(url, CreateContent(body)), ct).ConfigureAwait(false)).Content;

    public Task PatchAsync<TBody>(string url, TBody body, CancellationToken ct = default) =>
        SendCoreAsync(ApiRequest.Patch(url, CreateContent(body)), ct);
}
