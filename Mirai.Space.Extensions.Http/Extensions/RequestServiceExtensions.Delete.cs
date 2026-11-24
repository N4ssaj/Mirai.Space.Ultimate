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

        public async Task<T?> DeleteAsync<T>(string url, CancellationToken ct = default)
        {
            using var request = CreateRequest(HttpMethod.Delete, url);
            var response = await requests.SendAsync<T>(request, ct).ConfigureAwait(false);
            return response.Content;
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
            var response = await requests.SendAsync<TResponse>(request, ct).ConfigureAwait(false);
            return response.Content;
        }

        public async Task<RequestResponse> DeleteResponseAsync(string url, CancellationToken ct = default)
        {
            using var request = CreateRequest(HttpMethod.Delete, url);
            return await requests.SendAsync(request, ct).ConfigureAwait(false);
        }

        public async Task<RequestResponse<T>> DeleteResponseAsync<T>(
            string url,
            CancellationToken ct = default)
        {
            using var request = CreateRequest(HttpMethod.Delete, url);
            return await requests.SendAsync<T>(request, ct).ConfigureAwait(false);
        }

        public async Task<RequestResponse<TResponse>> DeleteResponseAsync<TResponse, TBody>(
            string url,
            TBody body,
            CancellationToken ct = default)
        {
            using var request = CreateRequest(HttpMethod.Delete, url, requests.CreateContent(body));
            return await requests.SendAsync<TResponse>(request, ct).ConfigureAwait(false);
        }
    }
}
