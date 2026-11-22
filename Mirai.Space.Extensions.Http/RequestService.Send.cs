using Mirai.Space.Extensions.Http.Abstractions;

namespace Mirai.Space.Extensions.Http;

internal sealed partial class RequestService
{
    public Task<ApiResponse> SendAsync(HttpRequestMessage request, CancellationToken ct = default) =>
        SendCoreAsync(request, disposeRequest: false, ct);

    public Task<ApiResponse<T>> SendAsync<T>(HttpRequestMessage request, CancellationToken ct = default) =>
        SendCoreAsync<T>(request, disposeRequest: false, ct);

    public Task<ApiResponse> SendStreamAsync(
        HttpRequestMessage request,
        Stream destination,
        CancellationToken ct = default) =>
        SendForStreamCoreAsync(request, destination, disposeRequest: false, ct);

    public IAsyncEnumerable<T> SendAsyncEnumerable<T>(
        HttpRequestMessage request,
        CancellationToken ct = default) =>
        SendAsyncEnumerableCoreAsync<T>(request, disposeRequest: false, ct);
}
