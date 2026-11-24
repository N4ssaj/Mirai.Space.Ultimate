using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Mirai.Space.Extensions.Http.Abstractions;
using Xunit;

namespace Mirai.Space.Extensions.Http.Tests;

public sealed class RequestServiceGetTests
{
    private const string ItemUrl = "http://localhost/items/1";

    [Fact]
    public async Task GetAsync_SendsGetRequestAndDeserializesBody()
    {
        using var host = TestHost.Create(_ => TestResponses.Json("""{"id":1,"name":"alpha"}"""));

        var result = await host.Client.Requests.GetAsync<TestModel>(
            ItemUrl, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(1, result.Id);
        Assert.Equal("alpha", result.Name);
        var request = Assert.Single(host.Handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal(new Uri(ItemUrl), request.RequestUri);
        Assert.Null(request.Content);
    }

    [Fact]
    public async Task GetResponseAsync_ReturnsStatusHeadersAndContent()
    {
        using var host = TestHost.Create(_ =>
        {
            var response = TestResponses.Json("""{"id":2,"name":"beta"}""");
            response.Headers.Add("X-Test", "get-response");
            response.Content.Headers.ContentLanguage.Add("en");
            return response;
        });

        var response = await host.Client.Requests.GetResponseAsync<TestModel>(
            ItemUrl, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("get-response", response.Headers["X-Test"].Single());
        Assert.Equal("en", response.ContentHeaders["Content-Language"].Single());
        Assert.NotNull(response.Content);
        Assert.Equal(2, response.Content.Id);
    }

    [Fact]
    public async Task GetAsync_NonSuccess_ThrowsRequestException()
    {
        using var host = TestHost.Create(_ => TestResponses.Empty(HttpStatusCode.NotFound));

        var exception = await Assert.ThrowsAsync<RequestException>(() =>
            host.Client.Requests.GetAsync<TestModel>(
                "http://localhost/missing", TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.NotFound, exception.StatusCode);
        Assert.Equal(HttpMethod.Get, exception.Method);
        Assert.Equal(new Uri("http://localhost/missing"), exception.RequestUri);
    }

    [Fact]
    public async Task GetAsync_NoContent_ReturnsDefault()
    {
        using var host = TestHost.Create(_ => TestResponses.Empty(HttpStatusCode.NoContent));

        var result = await host.Client.Requests.GetAsync<TestModel>(
            ItemUrl, TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetToStreamAsync_CopiesBodyIntoDestinationAndLeavesItOpen()
    {
        using var host = TestHost.Create(_ => TestResponses.Text("stream-body", "text/plain"));
        using var destination = new MemoryStream();

        var response = await host.Client.Requests.GetToStreamAsync(
            "http://localhost/stream", destination, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("stream-body", Encoding.UTF8.GetString(destination.ToArray()));
        Assert.True(destination.CanRead);
    }

    [Fact]
    public async Task GetToStreamAsync_NullDestination_ThrowsArgumentNullException()
    {
        using var host = TestHost.Create(_ => TestResponses.Empty());

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            host.Client.Requests.GetToStreamAsync(
                "http://localhost/stream", null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetAsync_ContentTypeHeaderIsNotRequired()
    {
        using var host = TestHost.Create(_ =>
        {
            var content = new ByteArrayContent(Encoding.UTF8.GetBytes("""{"id":3,"name":"gamma"}"""));
            content.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.test+json");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        });

        var result = await host.Client.Requests.GetAsync<TestModel>(
            ItemUrl, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(3, result.Id);
    }
}
