namespace Mirai.Space.Extensions.Http.Abstractions;

public interface IRequestService
{
    HttpContent CreateContent<TBody>(TBody body);

    Task<RequestResponse> SendAsync(HttpRequestMessage request, CancellationToken ct = default);

    Task<RequestResponse<T>> SendAsync<T>(HttpRequestMessage request, CancellationToken ct = default);

    Task<RequestResponse> SendToStreamAsync(
        HttpRequestMessage request,
        Stream destination,
        CancellationToken ct = default);

    IAsyncEnumerable<T> SendAsyncEnumerable<T>(HttpRequestMessage request, CancellationToken ct = default);
}
