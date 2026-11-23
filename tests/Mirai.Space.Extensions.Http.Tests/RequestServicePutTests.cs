using System.Net;
using Xunit;

namespace Mirai.Space.Extensions.Http.Tests;

public sealed class RequestServicePutTests
{
    private const string ResourceUrl = "http://localhost/items/1";

    [Fact]
    public async Task PutAsyncOfTResponseTBody_SendsPutAndReturnsContent()
    {
        using var host = TestHost.Create(_ => TestResponses.Json("""{"id":1,"name":"replaced"}"""));

        var result = await host.Client.Requests.PutAsync<TestModel, TestModel>(
            ResourceUrl, new TestModel(1, "payload"), TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal("replaced", result.Name);
        Assert.Equal(HttpMethod.Put, Assert.Single(host.Handler.Requests).Method);
    }

    [Fact]
    public async Task PutAsyncOfTBody_SendsPutAndCompletes()
    {
        using var host = TestHost.Create(_ => TestResponses.Empty());

        await host.Client.Requests.PutAsync(
            ResourceUrl, new TestModel(1, "payload"), TestContext.Current.CancellationToken);

        var request = Assert.Single(host.Handler.Requests);
        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.NotNull(request.Content);
        Assert.Equal("application/json", request.ContentType);
    }

    [Fact]
    public async Task PutResponseAsync_ReturnsEnvelope()
    {
        using var host = TestHost.Create(_ => TestResponses.Json("""{"id":1,"name":"replaced"}"""));

        var response = await host.Client.Requests.PutResponseAsync<TestModel, TestModel>(
            ResourceUrl, new TestModel(1, "payload"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(response.Content);
        Assert.Equal("replaced", response.Content.Name);
        Assert.Equal(HttpMethod.Put, Assert.Single(host.Handler.Requests).Method);
    }
}
