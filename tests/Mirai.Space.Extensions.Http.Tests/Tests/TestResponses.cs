using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace Mirai.Space.Extensions.Http.Tests;

internal static class TestResponses
{
    public static HttpResponseMessage Json(
        string body,
        HttpStatusCode statusCode = HttpStatusCode.OK) =>
        new(statusCode)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };

    public static HttpResponseMessage Empty(HttpStatusCode statusCode = HttpStatusCode.OK) =>
        new(statusCode);

    public static HttpResponseMessage Text(
        string body,
        string mediaType,
        HttpStatusCode statusCode = HttpStatusCode.OK) =>
        new(statusCode)
        {
            Content = new StringContent(body, Encoding.UTF8, mediaType),
        };

    public static HttpResponseMessage Bytes(
        byte[] body,
        string mediaType,
        HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var content = new ByteArrayContent(body);
        content.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
        return new HttpResponseMessage(statusCode) { Content = content };
    }
}
