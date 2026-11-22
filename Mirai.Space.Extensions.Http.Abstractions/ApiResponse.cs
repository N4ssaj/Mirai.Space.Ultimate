using System.Net;
using System.Net.Http.Headers;

namespace Mirai.Space.Extensions.Http.Abstractions;

public class ApiResponse
{
    public HttpStatusCode StatusCode { get; }

    public IReadOnlyDictionary<string, string[]> Headers { get; }

    public IReadOnlyDictionary<string, string[]> ContentHeaders { get; }

    internal ApiResponse(
        HttpStatusCode statusCode,
        IReadOnlyDictionary<string, string[]> headers,
        IReadOnlyDictionary<string, string[]> contentHeaders)
    {
        StatusCode = statusCode;
        Headers = headers;
        ContentHeaders = contentHeaders;
    }

    public static ApiResponse FromMessage(HttpResponseMessage response) =>
        new(response.StatusCode, Snapshot(response.Headers), Snapshot(response.Content.Headers));

    internal static IReadOnlyDictionary<string, string[]> Snapshot(HttpHeaders headers)
    {
        var result = new Dictionary<string, string[]>(
            headers.Count(),
            StringComparer.OrdinalIgnoreCase);

        foreach (var header in headers)
        {
            result[header.Key] = header.Value.ToArray();
        }

        return result;
    }
}

public sealed class ApiResponse<T> : ApiResponse
{
    public T? Content { get; }

    internal ApiResponse(
        HttpStatusCode statusCode,
        IReadOnlyDictionary<string, string[]> headers,
        IReadOnlyDictionary<string, string[]> contentHeaders,
        T? content)
        : base(statusCode, headers, contentHeaders)
    {
        Content = content;
    }

    public static ApiResponse<T> FromMessage(HttpResponseMessage response, T? content) =>
        new(response.StatusCode, Snapshot(response.Headers), Snapshot(response.Content.Headers), content);
}
