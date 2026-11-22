using System.Net;

namespace Mirai.Space.Extensions.Http.Abstractions;

public sealed class ApiException : Exception
{
    public HttpStatusCode StatusCode { get; }

    public HttpMethod? Method { get; }

    public Uri? RequestUri { get; }

    public ApiProblemDetails? Problem { get; }

    public string? ResponseBody { get; }

    public ApiException(
        string message,
        HttpStatusCode statusCode,
        HttpMethod? method,
        Uri? requestUri,
        ApiProblemDetails? problem = null,
        string? responseBody = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        Method = method;
        RequestUri = requestUri;
        Problem = problem;
        ResponseBody = responseBody;
    }
}
