using Mirai.Space.Extensions.Http.Abstractions;

namespace Mirai.Space.Extensions.Http;

public static partial class RequestServiceExtensions
{
    extension(IRequestService requests)
    {
        public async Task<TResponse?> PutAsync<TResponse, TBody>(
            string url,
            TBody body,
            CancellationToken ct = default)
        {
            using var request = CreateRequest(HttpMethod.Put, url, requests.CreateContent(body));
            var response = await requests.SendAsync<TResponse>(request, ct).ConfigureAwait(false);
            return response.Content;
        }

        public async Task<TResponse?> PutAsync<TResponse>(string url, CancellationToken ct = default)
        {
            using var request = CreateRequest(HttpMethod.Put, url);
            var response = await requests.SendAsync<TResponse>(request, ct).ConfigureAwait(false);
            return response.Content;
        }

        public async Task PutAsync<TBody>(string url, TBody body, CancellationToken ct = default)
        {
            using var request = CreateRequest(HttpMethod.Put, url, requests.CreateContent(body));
            await requests.SendAsync(request, ct).ConfigureAwait(false);
        }

        public async Task PutAsync(string url, CancellationToken ct = default)
        {
            using var request = CreateRequest(HttpMethod.Put, url);
            await requests.SendAsync(request, ct).ConfigureAwait(false);
        }

        public async Task<RequestResponse<TResponse>> PutResponseAsync<TResponse, TBody>(
            string url,
            TBody body,
            CancellationToken ct = default)
        {
            using var request = CreateRequest(HttpMethod.Put, url, requests.CreateContent(body));
            return await requests.SendAsync<TResponse>(request, ct).ConfigureAwait(false);
        }

        public async Task<RequestResponse> PutResponseAsync(string url, CancellationToken ct = default)
        {
            using var request = CreateRequest(HttpMethod.Put, url);
            return await requests.SendAsync(request, ct).ConfigureAwait(false);
        }

        public async Task<RequestResponse<TResponse>> PutResponseAsync<TResponse>(
            string url,
            CancellationToken ct = default)
        {
            using var request = CreateRequest(HttpMethod.Put, url);
            return await requests.SendAsync<TResponse>(request, ct).ConfigureAwait(false);
        }

        public async Task<RequestResponse> PutToStreamAsync(
            string url,
            Stream destination,
            CancellationToken ct = default)
        {
            using var request = CreateRequest(HttpMethod.Put, url);
            return await requests.SendToStreamAsync(request, destination, ct).ConfigureAwait(false);
        }

        public async Task<RequestResponse> PutToStreamAsync<TBody>(
            string url,
            TBody body,
            Stream destination,
            CancellationToken ct = default)
        {
            using var request = CreateRequest(HttpMethod.Put, url, requests.CreateContent(body));
            return await requests.SendToStreamAsync(request, destination, ct).ConfigureAwait(false);
        }
    }
}
