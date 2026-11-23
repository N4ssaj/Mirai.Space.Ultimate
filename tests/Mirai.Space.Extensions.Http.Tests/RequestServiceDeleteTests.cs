using System.Net;
using Xunit;

namespace Mirai.Space.Extensions.Http.Tests;

public sealed class RequestServiceDeleteTests
{
    private const string ResourceUrl = "http://localhost/items/1";

    [Fact]
    public async Task DeleteAsync_SendsDeleteAndCompletes()
    {
        using var host = TestHost.Create(_ => TestResponses.Empty());

        await host.Client.Requests.DeleteAsync(
            ResourceUrl, TestContext.Current.CancellationToken);

        var request = Assert.Single(host.Handler.Requests);
        Assert.Equal(HttpMethod.Delete, request.Method);
        Assert.Null(request.Content);
    }

    [Fact]
    public async Task DeleteAsyncOfT_SendsDeleteAndReturnsContent()
    {
        using var host = TestHost.Create(_ => TestResponses.Json("""{"id":1,"name":"deleted"}"""));

        var result = await host.Client.Requests.DeleteAsync<TestModel>(
            ResourceUrl, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal("deleted", result.Name);
        Assert.Equal(HttpMethod.Delete, Assert.Single(host.Handler.Requests).Method);
    }

    [Fact]
    public async Task DeleteResponseAsync_ReturnsEnvelope()
    {
        using var host = TestHost.Create(_ => TestResponses.Json("""{"id":1,"name":"deleted"}"""));

        var response = await host.Client.Requests.DeleteResponseAsync<TestModel>(
            ResourceUrl, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(response.Content);
        Assert.Equal("deleted", response.Content.Name);
        Assert.Equal(HttpMethod.Delete, Assert.Single(host.Handler.Requests).Method);
    }
}
