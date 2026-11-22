using Mirai.Space.Extensions.Http.Abstractions;

namespace Mirai.Space.Extensions.Http.Tests.About;

public sealed class AboutRequestService(IRequestService requests)
{
    private const string Resource = "about";
    private const string StreamResource = "about/stream";
    private const string TagsResource = "about/tags";

    public Task<AboutInfo?> GetAboutAsync(CancellationToken ct = default) =>
        requests.GetAsync<AboutInfo>(Resource, ct);

    public Task<ApiResponse<AboutInfo>> GetAboutResponseAsync(CancellationToken ct = default) =>
        requests.GetResponseAsync<AboutInfo>(Resource, ct);

    public Task<ApiResponse> DownloadAboutAsync(Stream destination, CancellationToken ct = default) =>
        requests.GetStreamAsync(StreamResource, destination, ct);

    public IAsyncEnumerable<string> GetTagsAsync(CancellationToken ct = default) =>
        requests.GetAsyncEnumerable<string>(TagsResource, ct);

    public Task<ApiResponse> HeadAboutAsync(CancellationToken ct = default) =>
        requests.HeadAsync(Resource, ct);

    public Task<ApiResponse> OptionsAboutAsync(CancellationToken ct = default) =>
        requests.OptionsAsync(Resource, ct);

    public Task<TResponse?> CreateAboutAsync<TResponse>(CancellationToken ct = default) =>
        requests.PostAsync<TResponse>(Resource, ct);

    public Task<TResponse?> CreateAboutAsync<TResponse, TBody>(TBody body, CancellationToken ct = default) =>
        requests.PostAsync<TResponse, TBody>(Resource, body, ct);

    public Task CreateAboutAsync(CancellationToken ct = default) =>
        requests.PostAsync(Resource, ct);

    public Task CreateAboutAsync<TBody>(TBody body, CancellationToken ct = default) =>
        requests.PostAsync(Resource, body, ct);

    public Task<ApiResponse<TResponse>> CreateAboutResponseAsync<TResponse, TBody>(
        TBody body,
        CancellationToken ct = default) =>
        requests.PostResponseAsync<TResponse, TBody>(Resource, body, ct);

    public Task<ApiResponse> CreateAboutStreamAsync(Stream destination, CancellationToken ct = default) =>
        requests.PostStreamAsync(StreamResource, destination, ct);

    public Task<ApiResponse> CreateAboutStreamAsync<TBody>(
        TBody body,
        Stream destination,
        CancellationToken ct = default) =>
        requests.PostStreamAsync(StreamResource, body, destination, ct);

    public Task<AboutInfo?> ReplaceAboutAsync<TBody>(TBody body, CancellationToken ct = default) =>
        requests.PutAsync<AboutInfo, TBody>(Resource, body, ct);

    public Task ReplaceAboutNoResponseAsync<TBody>(TBody body, CancellationToken ct = default) =>
        requests.PutAsync(Resource, body, ct);

    public Task<AboutInfo?> PatchAboutAsync<TBody>(TBody body, CancellationToken ct = default) =>
        requests.PatchAsync<AboutInfo, TBody>(Resource, body, ct);

    public Task PatchAboutNoResponseAsync<TBody>(TBody body, CancellationToken ct = default) =>
        requests.PatchAsync(Resource, body, ct);

    public Task DeleteAboutAsync(CancellationToken ct = default) =>
        requests.DeleteAsync(Resource, ct);

    public Task<T?> DeleteAboutAsync<T>(CancellationToken ct = default) =>
        requests.DeleteAsync<T>(Resource, ct);

    public Task<ApiResponse> SendAsync(HttpRequestMessage request, CancellationToken ct = default) =>
        requests.SendAsync(request, ct);

    public Task<ApiResponse<T>> SendAsync<T>(HttpRequestMessage request, CancellationToken ct = default) =>
        requests.SendAsync<T>(request, ct);

    public Task<ApiResponse> SendStreamAsync(
        HttpRequestMessage request,
        Stream destination,
        CancellationToken ct = default) =>
        requests.SendStreamAsync(request, destination, ct);

    public IAsyncEnumerable<T> SendAsyncEnumerable<T>(
        HttpRequestMessage request,
        CancellationToken ct = default) =>
        requests.SendAsyncEnumerable<T>(request, ct);
}
