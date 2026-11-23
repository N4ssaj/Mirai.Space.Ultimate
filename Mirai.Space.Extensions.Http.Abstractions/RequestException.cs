using System.Net;

namespace Mirai.Space.Extensions.Http.Abstractions;

public sealed class RequestException : Exception
{
    public HttpStatusCode StatusCode { get; }

    public HttpMethod? Method { get; }

    public Uri? RequestUri { get; }

    public RequestException(
        string message,
        HttpStatusCode statusCode,
        HttpMethod? method,
        Uri? requestUri,
        Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        Method = method;
        RequestUri = requestUri;
    }
}
