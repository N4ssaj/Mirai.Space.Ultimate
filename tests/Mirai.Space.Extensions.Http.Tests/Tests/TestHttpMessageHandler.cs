using System.Net.Http;

namespace Mirai.Space.Extensions.Http.Tests;

internal sealed record CapturedRequest(
    HttpMethod Method,
    Uri? RequestUri,
    string? ContentType,
    byte[]? Content,
    IReadOnlyDictionary<string, string[]> Headers);

internal sealed class TestHttpMessageHandler(
    Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
{
    public List<CapturedRequest> Requests { get; } = [];

    public List<HttpRequestMessage> RawRequests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var content = request.Content is null
            ? null
            : await request.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        var contentType = request.Content?.Headers.ContentType?.MediaType;
        var headers = request.Headers.ToDictionary(
            header => header.Key,
            header => header.Value.ToArray(),
            StringComparer.OrdinalIgnoreCase);

        Requests.Add(new CapturedRequest(request.Method, request.RequestUri, contentType, content, headers));
        RawRequests.Add(request);

        var response = responseFactory(request);
        response.RequestMessage ??= request;
        return response;
    }
}
