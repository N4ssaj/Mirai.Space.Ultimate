using System.Net;
using Xunit;

namespace Mirai.Space.Extensions.Http.Tests;

public sealed class RequestServicePatchTests
{
    private const string ResourceUrl = "http://localhost/items/1";

    [Fact]
    public async Task PatchAsyncOfTResponseTBody_SendsPatchAndReturnsContent()
    {
        using var host = TestHost.Create(_ => TestResponses.Json("""{"id":1,"name":"patched"}"""));

        var result = await host.Client.Requests.PatchAsync<TestModel, TestModel>(
            ResourceUrl, new TestModel(1, "payload"), TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal("patched", result.Name);
        Assert.Equal(HttpMethod.Patch, Assert.Single(host.Handler.Requests).Method);
    }

    [Fact]
    public async Task PatchAsyncOfTBody_SendsPatchAndCompletes()
    {
        using var host = TestHost.Create(_ => TestResponses.Empty());

        await host.Client.Requests.PatchAsync(
            ResourceUrl, new TestModel(1, "payload"), TestContext.Current.CancellationToken);

        var request = Assert.Single(host.Handler.Requests);
        Assert.Equal(HttpMethod.Patch, request.Method);
        Assert.NotNull(request.Content);
        Assert.Equal("application/json", request.ContentType);
    }

    [Fact]
    public async Task PatchResponseAsync_ReturnsEnvelope()
    {
        using var host = TestHost.Create(_ => TestResponses.Json("""{"id":1,"name":"patched"}"""));

        var response = await host.Client.Requests.PatchResponseAsync<TestModel, TestModel>(
            ResourceUrl, new TestModel(1, "payload"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(response.Content);
        Assert.Equal("patched", response.Content.Name);
        Assert.Equal(HttpMethod.Patch, Assert.Single(host.Handler.Requests).Method);
    }
}
