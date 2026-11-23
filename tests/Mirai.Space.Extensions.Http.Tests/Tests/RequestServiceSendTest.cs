using System.Net;
using Mirai.Space.Extensions.Http.Abstractions;
using Xunit;

namespace Mirai.Space.Extensions.Http.Tests;

public sealed class RequestServiceSendTest
{
    private const string ResourceUrl = "http://localhost/about";

    [Fact]
    public async Task SendAsync_SendsCallerRequestAndReturnsEnvelope()
    {
        using var host = TestHost.Create(_ => TestResponses.Json("""{"id":1,"name":"sent"}"""));
        using var request = new HttpRequestMessage(HttpMethod.Get, ResourceUrl);

        var response = await host.Client.Requests.SendAsync(
            request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(HttpMethod.Get, Assert.Single(host.Handler.Requests).Method);
    }

    [Fact]
    public async Task SendAsyncOfT_SendsCallerRequestAndReturnsContent()
    {
        using var host = TestHost.Create(_ => TestResponses.Json("""{"id":2,"name":"sent"}"""));
        using var request = new HttpRequestMessage(HttpMethod.Get, ResourceUrl);

        var response = await host.Client.Requests.SendAsync<TestModel>(
            request, TestContext.Current.CancellationToken);

        Assert.NotNull(response.Content);
        Assert.Equal(2, response.Content.Id);
        Assert.Equal(HttpMethod.Get, Assert.Single(host.Handler.Requests).Method);
    }

    [Fact]
    public async Task SendAsync_NonSuccess_ThrowsRequestException()
    {
        using var host = TestHost.Create(_ => TestResponses.Empty(HttpStatusCode.BadRequest));
        using var request = new HttpRequestMessage(HttpMethod.Get, ResourceUrl);

        var exception = await Assert.ThrowsAsync<RequestException>(() =>
            host.Client.Requests.SendAsync(request, TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
        Assert.Equal(HttpMethod.Get, exception.Method);
        Assert.Equal(new Uri(ResourceUrl), exception.RequestUri);
    }
}
