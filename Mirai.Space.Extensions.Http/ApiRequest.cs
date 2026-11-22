namespace Mirai.Space.Extensions.Http;

internal static class ApiRequest
{
    public static HttpRequestMessage Get(string url) => new(HttpMethod.Get, url);

    public static HttpRequestMessage Head(string url) => new(HttpMethod.Head, url);

    public static HttpRequestMessage Options(string url) => new(HttpMethod.Options, url);

    public static HttpRequestMessage Delete(string url) => new(HttpMethod.Delete, url);

    public static HttpRequestMessage Post(string url, HttpContent? content = null) =>
        new(HttpMethod.Post, url) { Content = content };

    public static HttpRequestMessage Put(string url, HttpContent? content = null) =>
        new(HttpMethod.Put, url) { Content = content };

    public static HttpRequestMessage Patch(string url, HttpContent? content = null) =>
        new(HttpMethod.Patch, url) { Content = content };
}
