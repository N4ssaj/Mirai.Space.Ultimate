using Mirai.Space.Extensions.Http.Abstractions;

namespace Mirai.Space.Extensions.Http;

public static partial class RequestServiceExtensions
{
    extension(IRequestService requests)
    {
        public async Task<RequestResponse> HeadAsync(string url, CancellationToken ct = default)
        {
            using var request = CreateRequest(HttpMethod.Head, url);
            return await requests.SendAsync(request, ct).ConfigureAwait(false);
        }

        public async Task<RequestResponse> OptionsAsync(string url, CancellationToken ct = default)
        {
            using var request = CreateRequest(HttpMethod.Options, url);
            return await requests.SendAsync(request, ct).ConfigureAwait(false);
        }
    }
}
