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
        Headers = headers;
        ContentHeaders = contentHeaders;
    }
}
