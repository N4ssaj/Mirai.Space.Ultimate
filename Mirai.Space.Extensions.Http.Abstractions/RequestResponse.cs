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

public sealed class RequestResponse<T> : RequestResponse
{
    public T? Content { get; }

    public RequestResponse(
        HttpStatusCode statusCode,
        IReadOnlyDictionary<string, IReadOnlyList<string>> headers,
        IReadOnlyDictionary<string, IReadOnlyList<string>> contentHeaders,
        T? content)
        : base(statusCode, headers, contentHeaders)
    {
        Content = content;
    }
}
