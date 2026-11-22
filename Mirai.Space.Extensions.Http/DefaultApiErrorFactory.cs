using Mirai.Space.Extensions.Http.Abstractions;

namespace Mirai.Space.Extensions.Http;

internal sealed class DefaultApiErrorFactory : IApiErrorFactory
{
    public ValueTask<ApiException> CreateAsync(HttpResponseMessage response, CancellationToken ct = default)
    {
        var request = response.RequestMessage;
        var message =
            $"HTTP {(int)response.StatusCode} {response.ReasonPhrase} for {request?.Method} {request?.RequestUri}.";

        return ValueTask.FromResult(new ApiException(
            message,
            response.StatusCode,
            request?.Method,
            request?.RequestUri));
    }
}
