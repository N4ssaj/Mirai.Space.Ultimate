using Mirai.Space.Extensions.Http.Abstractions;

namespace Mirai.Space.Extensions.Http;

public static partial class RequestServiceExtensions
{
    extension(IRequestService requests)
    {
        public async Task<RequestResponse> HeadResponseAsync(string url, CancellationToken ct = default)
        {
            using var request = CreateRequest(HttpMethod.Head, url);
            return await requests.SendResponseAsync(request, ct).ConfigureAwait(false);
        }

        public async Task<RequestResponse> OptionsResponseAsync(string url, CancellationToken ct = default)
        {
            using var request = CreateRequest(HttpMethod.Options, url);
            return await requests.SendResponseAsync(request, ct).ConfigureAwait(false);
        }
    }
}
