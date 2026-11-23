using System.Net;
using Mirai.Space.Extensions.Http.Abstractions;
using Mirai.Space.Extensions.Http.Extensions;
using Xunit;

namespace Mirai.Space.Extensions.Http.Tests;

public sealed class RequestServiceBehaviorTests
{
    private const string ResourceUrl = "http://localhost/items/1";

    [Theory]
    [InlineData(HttpStatusCode.NoContent)]
    [InlineData(HttpStatusCode.ResetContent)]
    public async Task GetAsync_BodylessStatus_ReturnsDefault(HttpStatusCode statusCode)
    {
        using var host = TestHost.Create(_ => TestResponses.Empty(statusCode));

        var result = await host.Client.Requests.GetAsync<TestModel>(
            ResourceUrl, TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAsync_ZeroContentLength_ReturnsDefault()
    {
        using var host = TestHost.Create(_ =>
        {
            var content = new ByteArrayContent([]);
            content.Headers.ContentLength = 0;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        });

        var result = await host.Client.Requests.GetAsync<TestModel>(
            ResourceUrl, TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetResponseAsync_HeaderLookupIsCaseInsensitive()
    {
        using var host = TestHost.Create(_ =>
        {
            var response = TestResponses.Empty();
            response.Headers.Add("X-Test", "value");
            return response;
        });

        var response = await host.Client.Requests.GetResponseAsync<TestModel>(
            ResourceUrl, TestContext.Current.CancellationToken);

        Assert.Equal("value", response.Headers["x-test"].Single());
    }

    [Fact]
    public async Task GetAsync_NonSuccess_InvokesErrorHandlerAndThrows()
    {
        var handler = new CapturingErrorHandler();
        using var host = TestHost.Create(
            _ => TestResponses.Empty(HttpStatusCode.InternalServerError),
            builder => builder.UseErrorHandler(handler));

        var exception = await Assert.ThrowsAsync<RequestException>(() =>
            host.Client.Requests.GetAsync<TestModel>(
                ResourceUrl, TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.InternalServerError, exception.StatusCode);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task GetAsync_NonSuccess_ErrorHandlerExceptionReplacesRequestException()
    {
        using var host = TestHost.Create(
            _ => TestResponses.Empty(HttpStatusCode.BadGateway),
            builder => builder.UseErrorHandler(new ThrowingErrorHandler()));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            host.Client.Requests.GetAsync<TestModel>(
                ResourceUrl, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetAsync_PreCancelledToken_ThrowsOperationCanceledException()
    {
        using var host = TestHost.Create(_ => TestResponses.Json("""{"id":1,"name":"a"}"""));
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            host.Client.Requests.GetAsync<TestModel>(ResourceUrl, source.Token));
    }

    [Fact]
    public async Task PostAsync_AfterCompletion_DisposesRequestContent()
    {
        using var host = TestHost.Create(_ => TestResponses.Empty());

        await host.Client.Requests.PostAsync(
            ResourceUrl, new TestModel(1, "payload"), TestContext.Current.CancellationToken);

        var request = Assert.Single(host.Handler.RawRequests);
        Assert.NotNull(request.Content);
        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            request.Content.ReadAsByteArrayAsync(CancellationToken.None));
    }

    [Fact]
    public async Task SendAsync_DoesNotDisposeCallerRequest()
    {
        using var host = TestHost.Create(_ => TestResponses.Empty());
        using var content = new TrackingContent([]);
        using var request = new HttpRequestMessage(HttpMethod.Post, ResourceUrl)
        {
            Content = content,
        };

        await host.Client.Requests.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.False(content.Disposed);
    }

    [Fact]
    public async Task GetAsync_NullUrl_ThrowsArgumentNullException()
    {
        using var host = TestHost.Create(_ => TestResponses.Empty());

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            host.Client.Requests.GetAsync<TestModel>(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetAsync_EmptyUnknownLengthBody_ReturnsDefault()
    {
        using var host = TestHost.Create(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(new MemoryStream())
        });

        var result = await host.Client.Requests.GetAsync<TestModel>(
            ResourceUrl, TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public void GetAsyncEnumerable_NullUrl_ThrowsBeforeEnumeration()
    {
        using var host = TestHost.Create(_ => TestResponses.Empty());

        Assert.Throws<ArgumentNullException>(() =>
            host.Client.Requests.GetAsyncEnumerable<int>(null!, TestContext.Current.CancellationToken));
    }

    private sealed class CapturingErrorHandler : IRequestErrorHandler
    {
        public int Calls { get; private set; }

        public ValueTask HandleAsync(RequestException exception, CancellationToken ct = default)
        {
            Calls++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ThrowingErrorHandler : IRequestErrorHandler
    {
        public ValueTask HandleAsync(RequestException exception, CancellationToken ct = default) =>
            throw new InvalidOperationException("handler failure");
    }

    private sealed class TrackingContent(byte[] bytes) : ByteArrayContent(bytes)
    {
        public bool Disposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }
}
