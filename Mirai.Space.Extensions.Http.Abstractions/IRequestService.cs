namespace Mirai.Space.Extensions.Http.Abstractions;

public interface IRequestService
{
    #region GET

    Task<T?> GetAsync<T>(string url, CancellationToken ct = default);

    Task<RequestResponse<T>> GetResponseAsync<T>(string url, CancellationToken ct = default);

    Task<RequestResponse> DownloadAsync(string url, Stream destination, CancellationToken ct = default);

    IAsyncEnumerable<T> GetAsyncEnumerable<T>(string url, CancellationToken ct = default);

    #endregion

    #region HEAD / OPTIONS

    Task<RequestResponse> HeadAsync(string url, CancellationToken ct = default);

    Task<RequestResponse> OptionsAsync(string url, CancellationToken ct = default);

    #endregion

    #region POST

    Task<T?> PostAsync<T>(string url, CancellationToken ct = default);

    Task<TResponse?> PostAsync<TResponse, TBody>(string url, TBody body, CancellationToken ct = default);

    Task PostAsync(string url, CancellationToken ct = default);

    Task PostAsync<TBody>(string url, TBody body, CancellationToken ct = default);

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

    #endregion

    #region PUT

    Task<TResponse?> PutAsync<TResponse, TBody>(string url, TBody body, CancellationToken ct = default);

    Task<TResponse?> PutAsync<TResponse>(string url, CancellationToken ct = default);

    Task PutAsync<TBody>(string url, TBody body, CancellationToken ct = default);

    Task PutAsync(string url, CancellationToken ct = default);

    Task<RequestResponse<TResponse>> PutResponseAsync<TResponse, TBody>(
        string url,
        TBody body,
        CancellationToken ct = default);

    Task<RequestResponse<TResponse>> PutResponseAsync<TResponse>(
        string url,
        CancellationToken ct = default);

    #endregion

    #region PATCH

    Task<TResponse?> PatchAsync<TResponse, TBody>(string url, TBody body, CancellationToken ct = default);

    Task<TResponse?> PatchAsync<TResponse>(string url, CancellationToken ct = default);

    Task PatchAsync<TBody>(string url, TBody body, CancellationToken ct = default);

    Task PatchAsync(string url, CancellationToken ct = default);

    Task<RequestResponse<TResponse>> PatchResponseAsync<TResponse, TBody>(
        string url,
        TBody body,
        CancellationToken ct = default);

    Task<RequestResponse<TResponse>> PatchResponseAsync<TResponse>(
        string url,
        CancellationToken ct = default);

    #endregion

    #region DELETE

    Task DeleteAsync(string url, CancellationToken ct = default);

    Task<T?> DeleteAsync<T>(string url, CancellationToken ct = default);

    Task DeleteAsync<TBody>(string url, TBody body, CancellationToken ct = default);

    Task<TResponse?> DeleteAsync<TResponse, TBody>(string url, TBody body, CancellationToken ct = default);

    Task<RequestResponse<T>> DeleteResponseAsync<T>(string url, CancellationToken ct = default);

    Task<RequestResponse<TResponse>> DeleteResponseAsync<TResponse, TBody>(
        string url,
        TBody body,
        CancellationToken ct = default);

    #endregion

    #region Escape hatches

    Task<RequestResponse> SendAsync(HttpRequestMessage request, CancellationToken ct = default);

    Task<RequestResponse<T>> SendAsync<T>(HttpRequestMessage request, CancellationToken ct = default);

    #endregion
}
