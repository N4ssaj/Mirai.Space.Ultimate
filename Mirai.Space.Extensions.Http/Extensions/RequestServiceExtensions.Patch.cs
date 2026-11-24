using Mirai.Space.Extensions.Http.Abstractions;

namespace Mirai.Space.Extensions.Http;

public static partial class RequestServiceExtensions
{
    extension(IRequestService requests)
    {
        public async Task<TResponse?> PatchAsync<TResponse, TBody>(
            string url,
            TBody body,
            CancellationToken ct = default)
        {
            using var request = CreateRequest(HttpMethod.Patch, url, requests.CreateContent(body));
            var response = await requests.SendAsync<TResponse>(request, ct).ConfigureAwait(false);
            return response.Content;
        }

        public async Task<TResponse?> PatchAsync<TResponse>(string url, CancellationToken ct = default)
        {
            using var request = CreateRequest(HttpMethod.Patch, url);
            var response = await requests.SendAsync<TResponse>(request, ct).ConfigureAwait(false);
            return response.Content;
        }

        public async Task PatchAsync<TBody>(string url, TBody body, CancellationToken ct = default)
        {
            using var request = CreateRequest(HttpMethod.Patch, url, requests.CreateContent(body));
            await requests.SendAsync(request, ct).ConfigureAwait(false);
        }

        public async Task PatchAsync(string url, CancellationToken ct = default)
        {
            using var request = CreateRequest(HttpMethod.Patch, url);
            await requests.SendAsync(request, ct).ConfigureAwait(false);
        }

        public async Task<RequestResponse<TResponse>> PatchResponseAsync<TResponse, TBody>(
            string url,
            TBody body,
            CancellationToken ct = default)
        {
            using var request = CreateRequest(HttpMethod.Patch, url, requests.CreateContent(body));
            return await requests.SendAsync<TResponse>(request, ct).ConfigureAwait(false);
        }

        public async Task<RequestResponse> PatchResponseAsync(string url, CancellationToken ct = default)
        {
            using var request = CreateRequest(HttpMethod.Patch, url);
            return await requests.SendAsync(request, ct).ConfigureAwait(false);
        }

        public async Task<RequestResponse<TResponse>> PatchResponseAsync<TResponse>(
            string url,
            CancellationToken ct = default)
        {
            using var request = CreateRequest(HttpMethod.Patch, url);
            return await requests.SendAsync<TResponse>(request, ct).ConfigureAwait(false);
        }

        public async Task<RequestResponse> PatchToStreamAsync(
            string url,
            Stream destination,
            CancellationToken ct = default)
        {
            using var request = CreateRequest(HttpMethod.Patch, url);
            return await requests.SendToStreamAsync(request, destination, ct).ConfigureAwait(false);
        }

        public async Task<RequestResponse> PatchToStreamAsync<TBody>(
            string url,
            TBody body,
            Stream destination,
            CancellationToken ct = default)
        {
            using var request = CreateRequest(HttpMethod.Patch, url, requests.CreateContent(body));
            return await requests.SendToStreamAsync(request, destination, ct).ConfigureAwait(false);
        }
    }
}
