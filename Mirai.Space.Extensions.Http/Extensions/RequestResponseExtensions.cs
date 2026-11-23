using System.Linq;
using System.Net.Http.Headers;
using Mirai.Space.Extensions.Http.Abstractions;

namespace Mirai.Space.Extensions.Http;

internal static class RequestResponseExtensions
{
    extension(HttpResponseMessage response)
    {
        public RequestResponse ToRequestResponse()
        {
            ArgumentNullException.ThrowIfNull(response);

            return new RequestResponse(
                response.StatusCode,
                CloneHeaders(response.Headers.NonValidated),
                CloneHeaders(response.Content.Headers.NonValidated));
        }

        public RequestResponse<T> ToRequestResponse<T>(T? content)
        {
            ArgumentNullException.ThrowIfNull(response);

            return new RequestResponse<T>(
                response.StatusCode,
                CloneHeaders(response.Headers.NonValidated),
                CloneHeaders(response.Content.Headers.NonValidated),
                content);
        }
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> CloneHeaders(
        HttpHeadersNonValidated headers) =>
        headers.ToDictionary(
            static header => header.Key,
            static header => (IReadOnlyList<string>)header.Value.ToArray(),
            StringComparer.OrdinalIgnoreCase);
}
