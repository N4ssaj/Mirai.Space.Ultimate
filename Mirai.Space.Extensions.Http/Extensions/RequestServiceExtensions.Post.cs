using Mirai.Space.Extensions.Http.Abstractions;

namespace Mirai.Space.Extensions.Http;

public static partial class RequestServiceExtensions
{
    extension(IRequestService requests)
    {
        public async Task<TResponse?> PostAsync<TResponse>(string url, CancellationToken ct = default)
        {
            using var request = CreateRequest(HttpMethod.Post, url);
            return await requests.SendAsync<TResponse>(request, ct).ConfigureAwait(false);
        }

        public async Task<TResponse?> PostAsync<TResponse, TBody>(
            string url,
            TBody body,
            CancellationToken ct = default)
        {
            using var request = CreateRequest(HttpMethod.Post, url, requests.CreateContent(body));
            return await requests.SendAsync<TResponse>(request, ct).ConfigureAwait(false);
        }

        public async Task PostAsync(string url, CancellationToken ct = default)
        {
            using var request = CreateRequest(HttpMethod.Post, url);
            await requests.SendAsync(request, ct).ConfigureAwait(false);
        }

        public async Task PostAsync<TBody>(string url, TBody body, CancellationToken ct = default)
        {
            using var request = CreateRequest(HttpMethod.Post, url, requests.CreateContent(body));
            await requests.SendAsync(request, ct).ConfigureAwait(false);
        }

        public async Task<RequestResponse> PostResponseAsync(string url, CancellationToken ct = default)
        {
            using var request = CreateRequest(HttpMethod.Post, url);
            return await requests.SendResponseAsync(request, ct).ConfigureAwait(false);
        }

        public async Task<RequestResponse<TResponse>> PostResponseAsync<TResponse>(
            string url,
            CancellationToken ct = default)
        {
            using var request = CreateRequest(HttpMethod.Post, url);
            return await requests.SendResponseAsync<TResponse>(request, ct).ConfigureAwait(false);
        }

        public async Task<RequestResponse<TResponse>> PostResponseAsync<TResponse, TBody>(
            string url,
            TBody body,
            CancellationToken ct = default)
        {
            using var request = CreateRequest(HttpMethod.Post, url, requests.CreateContent(body));
            return await requests.SendResponseAsync<TResponse>(request, ct).ConfigureAwait(false);
        }

        public async Task<RequestResponse> PostToStreamAsync(
            string url,
            Stream destination,
            CancellationToken ct = default)
        {
            using var request = CreateRequest(HttpMethod.Post, url);
            return await requests.SendToStreamAsync(request, destination, ct).ConfigureAwait(false);
        }

        public async Task<RequestResponse> PostToStreamAsync<TBody>(
            string url,
            TBody body,
            Stream destination,
            CancellationToken ct = default)
        {
            using var request = CreateRequest(HttpMethod.Post, url, requests.CreateContent(body));
            return await requests.SendToStreamAsync(request, destination, ct).ConfigureAwait(false);
        }
    }
}
