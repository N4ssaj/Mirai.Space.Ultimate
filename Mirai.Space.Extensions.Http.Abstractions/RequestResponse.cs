using System.Net;

namespace Mirai.Space.Extensions.Http.Abstractions;

public class RequestResponse
{
    public HttpStatusCode StatusCode { get; }

    public IReadOnlyDictionary<string, IReadOnlyList<string>> Headers { get; }

    public IReadOnlyDictionary<string, IReadOnlyList<string>> ContentHeaders { get; }

    public RequestResponse(
        HttpStatusCode statusCode,
        IReadOnlyDictionary<string, IReadOnlyList<string>> headers,
        IReadOnlyDictionary<string, IReadOnlyList<string>> contentHeaders)
    {
        StatusCode = statusCode;
        Headers = Snapshot(headers);
        ContentHeaders = Snapshot(contentHeaders);
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> Snapshot(
        IReadOnlyDictionary<string, IReadOnlyList<string>> headers) =>
        headers.ToDictionary(
            header => header.Key,
            header => (IReadOnlyList<string>)header.Value.ToArray(),
            StringComparer.OrdinalIgnoreCase);
}
