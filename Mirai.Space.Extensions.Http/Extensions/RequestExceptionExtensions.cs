using Mirai.Space.Extensions.Http.Abstractions;

namespace Mirai.Space.Extensions.Http;

internal static class RequestExceptionExtensions
{
    extension(HttpResponseMessage response)
    {
        public RequestException ToRequestException(HttpRequestMessage request)
        {
            ArgumentNullException.ThrowIfNull(response);
            ArgumentNullException.ThrowIfNull(request);

            var message = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase} for {request.Method} {request.RequestUri}.";

            return new RequestException(
                message,
                response.StatusCode,
                request.Method,
                request.RequestUri);
        }
    }
}
