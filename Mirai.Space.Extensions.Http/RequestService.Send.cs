using Mirai.Space.Extensions.Http.Abstractions;

namespace Mirai.Space.Extensions.Http;

internal sealed partial class RequestService
{
    public Task<RequestResponse> SendAsync(
        HttpRequestMessage message,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        return SendResponseCoreAsync(message, ct);
    }

    public Task<RequestResponse<T>> SendAsync<T>(
        HttpRequestMessage message,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        return SendResponseCoreAsync<T>(message, ct);
    }
}
