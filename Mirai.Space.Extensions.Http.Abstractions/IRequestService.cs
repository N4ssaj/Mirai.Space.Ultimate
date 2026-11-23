namespace Mirai.Space.Extensions.Http.Abstractions;

public interface IRequestService
{
    Task<T?> GetAsync<T>(string url, CancellationToken ct = default);

    Task<RequestResponse> GetResponseAsync(string url, CancellationToken ct = default);

    Task<RequestResponse<T>> GetResponseAsync<T>(string url, CancellationToken ct = default);

    Task<RequestResponse> DownloadAsync(string url, Stream destination, CancellationToken ct = default);

    IAsyncEnumerable<T> GetAsyncEnumerable<T>(string url, CancellationToken ct = default);

    Task<RequestResponse> HeadAsync(string url, CancellationToken ct = default);

    Task<RequestResponse> OptionsAsync(string url, CancellationToken ct = default);

    Task<T?> PostAsync<T>(string url, CancellationToken ct = default);

    Task<TResponse?> PostAsync<TResponse, TBody>(string url, TBody body, CancellationToken ct = default);

    Task PostAsync(string url, CancellationToken ct = default);

    Task PostAsync<TBody>(string url, TBody body, CancellationToken ct = default);

    Task<RequestResponse> PostResponseAsync(string url, CancellationToken ct = default);

    Task<RequestResponse<TResponse>> PostResponseAsync<TResponse>(
        string url,
        CancellationToken ct = default);

    Task<RequestResponse<TResponse>> PostResponseAsync<TResponse, TBody>(
        string url,
        TBody body,
        CancellationToken ct = default);

    Task<RequestResponse> PostToStreamAsync(string url, Stream destination, CancellationToken ct = default);

    Task<RequestResponse> PostToStreamAsync<TBody>(
        string url,
        TBody body,
        Stream destination,
        CancellationToken ct = default);

    Task<TResponse?> PutAsync<TResponse, TBody>(string url, TBody body, CancellationToken ct = default);

    Task<TResponse?> PutAsync<TResponse>(string url, CancellationToken ct = default);

    Task PutAsync<TBody>(string url, TBody body, CancellationToken ct = default);

    Task PutAsync(string url, CancellationToken ct = default);

    Task<RequestResponse<TResponse>> PutResponseAsync<TResponse, TBody>(
        string url,
        TBody body,
        CancellationToken ct = default);

    Task<RequestResponse> PutResponseAsync(string url, CancellationToken ct = default);

    Task<RequestResponse<TResponse>> PutResponseAsync<TResponse>(
        string url,
        CancellationToken ct = default);

    Task<RequestResponse> PutToStreamAsync(string url, Stream destination, CancellationToken ct = default);

    Task<RequestResponse> PutToStreamAsync<TBody>(
        string url,
        TBody body,
        Stream destination,
        CancellationToken ct = default);

    Task<TResponse?> PatchAsync<TResponse, TBody>(string url, TBody body, CancellationToken ct = default);

    Task<TResponse?> PatchAsync<TResponse>(string url, CancellationToken ct = default);

    Task PatchAsync<TBody>(string url, TBody body, CancellationToken ct = default);

    Task PatchAsync(string url, CancellationToken ct = default);

    Task<RequestResponse<TResponse>> PatchResponseAsync<TResponse, TBody>(
        string url,
        TBody body,
        CancellationToken ct = default);

    Task<RequestResponse> PatchResponseAsync(string url, CancellationToken ct = default);

    Task<RequestResponse<TResponse>> PatchResponseAsync<TResponse>(
        string url,
        CancellationToken ct = default);

    Task<RequestResponse> PatchToStreamAsync(string url, Stream destination, CancellationToken ct = default);

    Task<RequestResponse> PatchToStreamAsync<TBody>(
        string url,
        TBody body,
        Stream destination,
        CancellationToken ct = default);

    Task DeleteAsync(string url, CancellationToken ct = default);

    Task<T?> DeleteAsync<T>(string url, CancellationToken ct = default);

    Task DeleteAsync<TBody>(string url, TBody body, CancellationToken ct = default);

    Task<TResponse?> DeleteAsync<TResponse, TBody>(string url, TBody body, CancellationToken ct = default);

    Task<RequestResponse> DeleteResponseAsync(string url, CancellationToken ct = default);

    Task<RequestResponse<T>> DeleteResponseAsync<T>(string url, CancellationToken ct = default);

    Task<RequestResponse<TResponse>> DeleteResponseAsync<TResponse, TBody>(
        string url,
        TBody body,
        CancellationToken ct = default);

    Task<RequestResponse> SendAsync(HttpRequestMessage request, CancellationToken ct = default);

    Task<RequestResponse<T>> SendAsync<T>(HttpRequestMessage request, CancellationToken ct = default);
}
