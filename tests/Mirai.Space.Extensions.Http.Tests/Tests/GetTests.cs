using System.Net;
using System.Text;
using Mirai.Space.Extensions.Http.Abstractions;
using Mirai.Space.Extensions.Http.Json;
using Xunit;

namespace Mirai.Space.Extensions.Http.Tests;

public sealed class GetTests
{
    [Fact]
    public async Task GetAsync_DeserializesJsonBody()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"id":1,"name":"alpha"}""", Encoding.UTF8, "application/json"),
        });
        var service = CreateService(handler);

        var result = await service.GetAsync<TestModel>(
            "http://localhost/items/1", TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(1, result.Id);
        Assert.Equal("alpha", result.Name);
    }

    [Fact]
    public async Task GetAsync_NonSuccess_ThrowsApiException()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var service = CreateService(handler);

        var exception = await Assert.ThrowsAsync<ApiException>(
            () => service.GetAsync<TestModel>(
                "http://localhost/missing", TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.NotFound, exception.StatusCode);
        Assert.Equal(HttpMethod.Get, exception.Method);
        Assert.Equal(new Uri("http://localhost/missing"), exception.RequestUri);
    }

    [Fact]
    public async Task GetAsync_NoContent_ReturnsDefault()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
        var service = CreateService(handler);

        var result = await service.GetAsync<TestModel>(
            "http://localhost/empty", TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task PostAsync_SetsJsonContentTypeAndBody()
    {
        string? capturedMediaType = null;
        string? capturedBody = null;
        var handler = new StubHandler(request =>
        {
            capturedMediaType = request.Content?.Headers.ContentType?.MediaType;
            capturedBody = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"id":2,"name":"beta"}""", Encoding.UTF8, "application/json"),
            };
        });
        var service = CreateService(handler);

        var result = await service.PostAsync<TestModel, TestModel>(
            "http://localhost/items", new TestModel(0, "beta"), TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(2, result.Id);
        Assert.Equal("application/json", capturedMediaType);
        Assert.Contains("\"name\":\"beta\"", capturedBody);
    }

    private static RequestService CreateService(HttpMessageHandler handler) =>
        new(new HttpClient(handler), new JsonApiSerializer(), new TestErrorFactory());
}
