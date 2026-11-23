using System.Net;

namespace Mirai.Space.Extensions.Http.Abstractions;

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
