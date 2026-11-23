using System.Runtime.CompilerServices;
using Mirai.Space.Extensions.Http.Abstractions;

namespace Mirai.Space.Extensions.Http;

internal sealed partial class RequestService
{
    public async Task<T?> GetAsync<T>(string url, CancellationToken ct = default)
    {
        using var request = CreateRequest(HttpMethod.Get, url);
        return await SendContentCoreAsync<T>(request, ct).ConfigureAwait(false);
    }

    public async Task<RequestResponse<T>> GetResponseAsync<T>(string url, CancellationToken ct = default)
    {
        using var request = CreateRequest(HttpMethod.Get, url);
        return await SendResponseCoreAsync<T>(request, ct).ConfigureAwait(false);
    }

    public async Task<RequestResponse> DownloadAsync(
        string url,
        Stream destination,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(destination);

        using var request = CreateRequest(HttpMethod.Get, url);
        return await SendStreamCoreAsync(request, destination, ct).ConfigureAwait(false);
    }

    public IAsyncEnumerable<T> GetAsyncEnumerable<T>(string url, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(url);

        return GetAsyncEnumerableCoreAsync<T>(url, ct);
    }

    private async IAsyncEnumerable<T> GetAsyncEnumerableCoreAsync<T>(
        string url,
        [EnumeratorCancellation] CancellationToken ct)
    {
        using var request = CreateRequest(HttpMethod.Get, url);

        await foreach (var item in SendAsyncEnumerableCoreAsync<T>(request, ct)
                           .WithCancellation(ct)
                           .ConfigureAwait(false))
        {
            yield return item;
        }
    }
}
