namespace Mirai.Space.Extensions.Http.Abstractions;

public interface IRequestService
{
    HttpContent CreateContent<TBody>(TBody body);

    Task SendAsync(HttpRequestMessage request, CancellationToken ct = default);

    Task<TResponse?> SendAsync<TResponse>(HttpRequestMessage request, CancellationToken ct = default);

    Task<RequestResponse> SendResponseAsync(HttpRequestMessage request, CancellationToken ct = default);

    Task<RequestResponse<TResponse>> SendResponseAsync<TResponse>(
        HttpRequestMessage request,
        CancellationToken ct = default);

    Task<RequestResponse> SendToStreamAsync(
        HttpRequestMessage request,
        Stream destination,
        CancellationToken ct = default);

    IAsyncEnumerable<T> SendAsyncEnumerable<T>(HttpRequestMessage request, CancellationToken ct = default);
}
