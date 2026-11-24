using Mirai.Space.Extensions.Http.Abstractions;

namespace Mirai.Space.Extensions.Http;

public static partial class RequestServiceExtensions
{
    extension(IRequestService requests)
    {
        public async Task DeleteAsync(string url, CancellationToken ct = default)
        {
            using var request = CreateRequest(HttpMethod.Delete, url);
            await requests.SendAsync(request, ct).ConfigureAwait(false);
        }

        public async Task<TResponse?> DeleteAsync<TResponse>(string url, CancellationToken ct = default)
        {
            using var request = CreateRequest(HttpMethod.Delete, url);
            return await requests.SendAsync<TResponse>(request, ct).ConfigureAwait(false);
        }

        public async Task DeleteAsync<TBody>(string url, TBody body, CancellationToken ct = default)
        {
            using var request = CreateRequest(HttpMethod.Delete, url, requests.CreateContent(body));
            await requests.SendAsync(request, ct).ConfigureAwait(false);
        }

        public async Task<TResponse?> DeleteAsync<TResponse, TBody>(
            string url,
            TBody body,
            CancellationToken ct = default)
        {
            using var request = CreateRequest(HttpMethod.Delete, url, requests.CreateContent(body));
            return await requests.SendAsync<TResponse>(request, ct).ConfigureAwait(false);
        }

        public async Task<RequestResponse> DeleteResponseAsync(string url, CancellationToken ct = default)
        {
            using var request = CreateRequest(HttpMethod.Delete, url);
            return await requests.SendResponseAsync(request, ct).ConfigureAwait(false);
        }

        public async Task<RequestResponse<TResponse>> DeleteResponseAsync<TResponse>(
            string url,
            CancellationToken ct = default)
        {
            using var request = CreateRequest(HttpMethod.Delete, url);
            return await requests.SendResponseAsync<TResponse>(request, ct).ConfigureAwait(false);
        }

        public async Task<RequestResponse<TResponse>> DeleteResponseAsync<TResponse, TBody>(
            string url,
            TBody body,
            CancellationToken ct = default)
        {
            using var request = CreateRequest(HttpMethod.Delete, url, requests.CreateContent(body));
            return await requests.SendResponseAsync<TResponse>(request, ct).ConfigureAwait(false);
        }

        public async Task<RequestResponse> DeleteToStreamAsync(
            string url,
            Stream destination,
            CancellationToken ct = default)
        {
            using var request = CreateRequest(HttpMethod.Delete, url);
            return await requests.SendToStreamAsync(request, destination, ct).ConfigureAwait(false);
        }

        public async Task<RequestResponse> DeleteToStreamAsync<TBody>(
            string url,
            TBody body,
            Stream destination,
            CancellationToken ct = default)
        {
            using var request = CreateRequest(HttpMethod.Delete, url, requests.CreateContent(body));
            return await requests.SendToStreamAsync(request, destination, ct).ConfigureAwait(false);
        }
    }
}
