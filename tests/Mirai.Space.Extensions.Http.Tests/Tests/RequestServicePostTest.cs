using System.Net;
using System.Text;
using Xunit;

namespace Mirai.Space.Extensions.Http.Tests;

public sealed class RequestServicePostTest
{
    private const string ResourceUrl = "http://localhost/items";

    [Fact]
    public async Task PostAsyncOfT_NoBody_SendsPostAndReturnsContent()
    {
        using var host = TestHost.Create(_ => TestResponses.Json("""{"id":1,"name":"created"}"""));

        var result = await host.Client.Requests.PostAsync<TestModel>(
            ResourceUrl, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(1, result.Id);
        AssertPostRequest(host, hasBody: false);
    }

    [Fact]
    public async Task PostAsyncOfTResponseTBody_SendsBodyAndReturnsContent()
    {
        using var host = TestHost.Create(_ => TestResponses.Json("""{"id":2,"name":"created"}"""));

        var result = await host.Client.Requests.PostAsync<TestModel, TestModel>(
            ResourceUrl, new TestModel(0, "payload"), TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(2, result.Id);
        AssertPostRequest(host, hasBody: true);
        Assert.Contains("\"name\":\"payload\"", Encoding.UTF8.GetString(host.Handler.Requests[^1].Content!));
    }

    [Fact]
    public async Task PostAsync_NoBody_SendsPostAndCompletes()
    {
        using var host = TestHost.Create(_ => TestResponses.Empty());

        await host.Client.Requests.PostAsync(
            ResourceUrl, TestContext.Current.CancellationToken);

        AssertPostRequest(host, hasBody: false);
    }

    [Fact]
    public async Task PostAsyncOfTBody_SendsBodyAndCompletes()
    {
        using var host = TestHost.Create(_ => TestResponses.Empty());

        await host.Client.Requests.PostAsync(
            ResourceUrl, new TestModel(0, "payload"), TestContext.Current.CancellationToken);

        AssertPostRequest(host, hasBody: true);
    }

    [Fact]
    public async Task PostResponseAsyncOfT_NoBody_ReturnsEnvelope()
    {
        using var host = TestHost.Create(_ => TestResponses.Json("""{"id":3,"name":"created"}"""));

        var response = await host.Client.Requests.PostResponseAsync<TestModel>(
            ResourceUrl, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(response.Content);
        Assert.Equal(3, response.Content.Id);
        AssertPostRequest(host, hasBody: false);
    }

    [Fact]
    public async Task PostResponseAsyncOfTResponseTBody_SendsBodyAndReturnsEnvelope()
    {
        using var host = TestHost.Create(_ => TestResponses.Json("""{"id":4,"name":"created"}"""));

        var response = await host.Client.Requests.PostResponseAsync<TestModel, TestModel>(
            ResourceUrl, new TestModel(0, "payload"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(response.Content);
        Assert.Equal(4, response.Content.Id);
        AssertPostRequest(host, hasBody: true);
    }

    [Fact]
    public async Task PostToStreamAsync_NoBody_CopiesContentIntoDestination()
    {
        using var host = TestHost.Create(_ => TestResponses.Text("post-stream", "text/plain"));
        using var destination = new MemoryStream();

        var response = await host.Client.Requests.PostToStreamAsync(
            ResourceUrl, destination, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("post-stream", Encoding.UTF8.GetString(destination.ToArray()));
        Assert.True(destination.CanRead);
        AssertPostRequest(host, hasBody: false);
    }

    [Fact]
    public async Task PostToStreamAsyncOfTBody_SendsBodyAndCopiesContentIntoDestination()
    {
        using var host = TestHost.Create(_ => TestResponses.Text("post-stream", "text/plain"));
        using var destination = new MemoryStream();

        var response = await host.Client.Requests.PostToStreamAsync(
            ResourceUrl,
            new TestModel(0, "payload"),
            destination,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("post-stream", Encoding.UTF8.GetString(destination.ToArray()));
        AssertPostRequest(host, hasBody: true);
    }

    private static void AssertPostRequest(TestHost host, bool hasBody)
    {
        var request = Assert.Single(host.Handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(new Uri(ResourceUrl), request.RequestUri);
        if (hasBody)
        {
            Assert.NotNull(request.Content);
            Assert.Equal("application/json", request.ContentType);
        }
        else
        {
            Assert.Null(request.Content);
        }
    }
}
