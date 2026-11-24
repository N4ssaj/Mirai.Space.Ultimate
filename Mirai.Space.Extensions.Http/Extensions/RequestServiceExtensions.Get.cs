using Mirai.Space.Extensions.Http.Abstractions;

namespace Mirai.Space.Extensions.Http;

public static partial class RequestServiceExtensions
{
    extension(IRequestService requests)
    {
        public async Task<T?> GetAsync<T>(string url, CancellationToken ct = default)
        {
            using var request = CreateRequest(HttpMethod.Get, url);
            var response = await requests.SendAsync<T>(request, ct).ConfigureAwait(false);
            return response.Content;
        }

        public async Task<RequestResponse> GetResponseAsync(string url, CancellationToken ct = default)
        {
            using var request = CreateRequest(HttpMethod.Get, url);
            return await requests.SendAsync(request, ct).ConfigureAwait(false);
        }

        public async Task<RequestResponse<T>> GetResponseAsync<T>(string url, CancellationToken ct = default)
        {
            using var request = CreateRequest(HttpMethod.Get, url);
            return await requests.SendAsync<T>(request, ct).ConfigureAwait(false);
        }

        public async Task<RequestResponse> DownloadAsync(
            string url,
            Stream destination,
            CancellationToken ct = default)
        {
            using var request = CreateRequest(HttpMethod.Get, url);
            return await requests.SendToStreamAsync(request, destination, ct).ConfigureAwait(false);
        }

        public IAsyncEnumerable<T> GetAsyncEnumerable<T>(string url, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(url);
            return GetAsyncEnumerableCore<T>(requests, url, ct);
        }
    }
}
