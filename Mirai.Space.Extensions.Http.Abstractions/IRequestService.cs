namespace Mirai.Space.Extensions.Http.Abstractions;

public interface IRequestService
{
    Task<T?> GetAsync<T>(string url, CancellationToken ct = default);

    Task<ApiResponse<T>> GetResponseAsync<T>(string url, CancellationToken ct = default);

    Task<StreamedApiResponse> GetStreamAsync(string url, CancellationToken ct = default);

    IAsyncEnumerable<T> GetAsyncEnumerable<T>(string url, CancellationToken ct = default);

    Task<ApiResponse> HeadAsync(string url, CancellationToken ct = default);

    Task<ApiResponse> OptionsAsync(string url, CancellationToken ct = default);

    Task<T?> PostAsync<T>(string url, CancellationToken ct = default);

    Task<TResponse?> PostAsync<TResponse, TBody>(string url, TBody body, CancellationToken ct = default);

    Task PostAsync(string url, CancellationToken ct = default);

    Task PostAsync<TBody>(string url, TBody body, CancellationToken ct = default);

    Task<ApiResponse<TResponse>> PostResponseAsync<TResponse, TBody>(
        string url,
        TBody body,
        CancellationToken ct = default);

    Task<StreamedApiResponse> PostStreamAsync(string url, CancellationToken ct = default);

    Task<StreamedApiResponse> PostStreamAsync<TBody>(string url, TBody body, CancellationToken ct = default);

    Task<TResponse?> PutAsync<TResponse, TBody>(string url, TBody body, CancellationToken ct = default);

    Task PutAsync<TBody>(string url, TBody body, CancellationToken ct = default);

    Task<TResponse?> PatchAsync<TResponse, TBody>(string url, TBody body, CancellationToken ct = default);

    Task PatchAsync<TBody>(string url, TBody body, CancellationToken ct = default);

    Task DeleteAsync(string url, CancellationToken ct = default);

    Task<T?> DeleteAsync<T>(string url, CancellationToken ct = default);

    Task<ApiResponse> SendAsync(HttpRequestMessage request, CancellationToken ct = default);

    Task<ApiResponse<T>> SendAsync<T>(HttpRequestMessage request, CancellationToken ct = default);

    Task<StreamedApiResponse> SendStreamAsync(HttpRequestMessage request, CancellationToken ct = default);

    IAsyncEnumerable<T> SendAsyncEnumerable<T>(HttpRequestMessage request, CancellationToken ct = default);
}
