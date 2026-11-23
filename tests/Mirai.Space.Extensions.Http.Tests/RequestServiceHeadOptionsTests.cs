using System.Net;
using Mirai.Space.Extensions.Http.Abstractions;
using Xunit;

namespace Mirai.Space.Extensions.Http.Tests;

public sealed class RequestServiceHeadOptionsTests
{
    private const string ResourceUrl = "http://localhost/about";

    [Fact]
    public async Task HeadAsync_SendsHeadRequestAndReturnsHeaders()
    {
        using var host = TestHost.Create(_ =>
        {
            var response = TestResponses.Empty();
            response.Headers.Add("X-Test", "head");
            return response;
        });

        var response = await host.Client.Requests.HeadAsync(
            ResourceUrl, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("head", response.Headers["X-Test"].Single());
        Assert.Equal(HttpMethod.Head, Assert.Single(host.Handler.Requests).Method);
    }

    [Fact]
    public async Task OptionsAsync_SendsOptionsRequestAndReturnsContentHeaders()
    {
        using var host = TestHost.Create(_ =>
        {
            var response = TestResponses.Empty();
            response.Content = new ByteArrayContent([]);
            response.Content.Headers.Allow.Add("GET");
            response.Content.Headers.Allow.Add("POST");
            return response;
        });

        var response = await host.Client.Requests.OptionsAsync(
            ResourceUrl, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.ContentHeaders.TryGetValue("Allow", out var allow));
        Assert.Equal(new[] { "GET", "POST" }, allow);
        Assert.Equal(HttpMethod.Options, Assert.Single(host.Handler.Requests).Method);
    }

    [Fact]
    public async Task OptionsAsync_EmptyResponse_DoesNotThrow()
    {
        using var host = TestHost.Create(_ => TestResponses.Empty());

        var response = await host.Client.Requests.OptionsAsync(
            ResourceUrl, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.ContentHeaders.ContainsKey("Allow"));
    }

    [Fact]
    public async Task HeadAsync_NonSuccess_ThrowsRequestException()
    {
        using var host = TestHost.Create(_ => TestResponses.Empty(HttpStatusCode.MethodNotAllowed));

        var exception = await Assert.ThrowsAsync<RequestException>(() =>
            host.Client.Requests.HeadAsync(
                ResourceUrl, TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.MethodNotAllowed, exception.StatusCode);
        Assert.Equal(HttpMethod.Head, exception.Method);
    }
}
