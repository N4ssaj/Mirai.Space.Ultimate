using Mirai.Space.Extensions.Http.Abstractions;

namespace Mirai.Space.Extensions.Http;

internal sealed partial class RequestService
{
    public async Task<RequestResponse> HeadAsync(string url, CancellationToken ct = default)
    {
        using var request = CreateRequest(HttpMethod.Head, url);
        return await SendResponseCoreAsync(request, ct).ConfigureAwait(false);
    }
}
