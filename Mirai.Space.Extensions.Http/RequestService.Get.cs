using Mirai.Space.Extensions.Http.Abstractions;

namespace Mirai.Space.Extensions.Http;

public sealed partial class RequestService
{
    public async Task<T?> GetAsync<T>(string url, CancellationToken ct = default) =>
        (await SendCoreAsync<T>(ApiRequest.Get(url), ct).ConfigureAwait(false)).Content;

    public Task<ApiResponse<T>> GetResponseAsync<T>(string url, CancellationToken ct = default) =>
        SendCoreAsync<T>(ApiRequest.Get(url), ct);

    public Task<StreamedApiResponse> GetStreamAsync(string url, CancellationToken ct = default) =>
        SendForStreamCoreAsync(ApiRequest.Get(url), disposeRequest: true, ct);

    public IAsyncEnumerable<T> GetAsyncEnumerable<T>(string url, CancellationToken ct = default) =>
        SendAsyncEnumerableCoreAsync<T>(ApiRequest.Get(url), disposeRequest: true, ct);

    public Task<ApiResponse> HeadAsync(string url, CancellationToken ct = default) =>
        SendCoreAsync(ApiRequest.Head(url), ct);

    public Task<ApiResponse> OptionsAsync(string url, CancellationToken ct = default) =>
        SendCoreAsync(ApiRequest.Options(url), ct);

    public Task DeleteAsync(string url, CancellationToken ct = default) =>
        SendCoreAsync(ApiRequest.Delete(url), ct);

    public async Task<T?> DeleteAsync<T>(string url, CancellationToken ct = default) =>
        (await SendCoreAsync<T>(ApiRequest.Delete(url), ct).ConfigureAwait(false)).Content;
}
