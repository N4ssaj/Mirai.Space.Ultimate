using Mirai.Space.Extensions.Http.Abstractions;

namespace Mirai.Space.Extensions.Http;

public static partial class RequestServiceExtensions
{
    extension(IRequestService requests)
    {
        public async Task<TResponse?> GetAsync<TResponse>(string url, CancellationToken ct = default)
        {
            using var request = CreateRequest(HttpMethod.Get, url);
            return await requests.SendAsync<TResponse>(request, ct).ConfigureAwait(false);
        }

        public async Task<RequestResponse> GetResponseAsync(string url, CancellationToken ct = default)
        {
            using var request = CreateRequest(HttpMethod.Get, url);
            return await requests.SendResponseAsync(request, ct).ConfigureAwait(false);
        }

        public async Task<RequestResponse<TResponse>> GetResponseAsync<TResponse>(
            string url,
            CancellationToken ct = default)
        {
            using var request = CreateRequest(HttpMethod.Get, url);
            return await requests.SendResponseAsync<TResponse>(request, ct).ConfigureAwait(false);
        }

        public async Task<RequestResponse> GetToStreamAsync(
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
