using System.Net;
using System.Net.Http.Headers;
using Mirai.Space.Extensions.Http.Abstractions;
using Mirai.Space.Extensions.Http.Handlers;
using Xunit;

namespace Mirai.Space.Extensions.Http.Tests;

public sealed class AuthorizationTests
{
    [Fact]
    public async Task AuthorizationHandler_AppliesToken()
    {
        var ct = TestContext.Current.CancellationToken;
        var provider = new StubAuthorizationProvider(new AuthenticationHeaderValue("Bearer", "token"));
        var recorder = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        using var handler = new ApiAuthorizationHandler(provider) { InnerHandler = recorder };
        using var http = new HttpClient(handler);

        using var response = await http.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, "http://localhost/secure"), ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Bearer token", recorder.Requests[0].Headers.Authorization?.ToString());
    }

    [Fact]
    public async Task AuthorizationHandler_ClearsStaleHeader()
    {
        var ct = TestContext.Current.CancellationToken;
        var provider = new StubAuthorizationProvider(null);
        var recorder = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        using var handler = new ApiAuthorizationHandler(provider) { InnerHandler = recorder };
        using var http = new HttpClient(handler);

        using var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/public");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "stale");
        using var response = await http.SendAsync(request, ct);

        Assert.Null(recorder.Requests[0].Headers.Authorization);
    }

    [Fact]
    public async Task Reauthentication_401_RetriesWithRefreshedToken()
    {
        var ct = TestContext.Current.CancellationToken;
        var provider = new StubAuthorizationProvider(new AuthenticationHeaderValue("Bearer", "old-token"));
        var calls = 0;
        var recorder = new RecordingHandler(_ =>
        {
            calls++;
            return calls == 1
                ? new HttpResponseMessage(HttpStatusCode.Unauthorized)
                : new HttpResponseMessage(HttpStatusCode.OK);
        });

        var reauthenticator = new StubReauthenticator(provider, result: true, token: "new-token");
        using var authorization = new ApiAuthorizationHandler(provider) { InnerHandler = recorder };
        using var reauthentication = new ApiReauthenticationHandler(reauthenticator)
        {
            InnerHandler = authorization,
        };
        using var http = new HttpClient(reauthentication);

        using var response = await http.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, "http://localhost/secure"), ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, reauthenticator.Calls);
        Assert.Equal(2, recorder.Requests.Count);
        Assert.Equal("Bearer new-token", recorder.Requests[1].Headers.Authorization?.ToString());
    }

    [Fact]
    public async Task Reauthentication_401_ReauthenticatorFails_NoRetry()
    {
        var ct = TestContext.Current.CancellationToken;
        var provider = new StubAuthorizationProvider(new AuthenticationHeaderValue("Bearer", "old-token"));
        var recorder = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var reauthenticator = new StubReauthenticator(provider, result: false);

        using var authorization = new ApiAuthorizationHandler(provider) { InnerHandler = recorder };
        using var reauthentication = new ApiReauthenticationHandler(reauthenticator)
        {
            InnerHandler = authorization,
        };
        using var http = new HttpClient(reauthentication);

        using var response = await http.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, "http://localhost/secure"), ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(1, reauthenticator.Calls);
        Assert.Single(recorder.Requests);
    }

    [Fact]
    public async Task Reauthentication_StreamContent_IsNotRetried()
    {
        var ct = TestContext.Current.CancellationToken;
        var provider = new StubAuthorizationProvider(new AuthenticationHeaderValue("Bearer", "old-token"));
        var recorder = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var reauthenticator = new StubReauthenticator(provider, result: true, token: "new-token");

        using var authorization = new ApiAuthorizationHandler(provider) { InnerHandler = recorder };
        using var reauthentication = new ApiReauthenticationHandler(reauthenticator)
        {
            InnerHandler = authorization,
        };
        using var http = new HttpClient(reauthentication);

        using var content = new StreamContent(new MemoryStream([1, 2, 3]));
        using var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost/upload") { Content = content };
        using var response = await http.SendAsync(request, ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, reauthenticator.Calls);
        Assert.Single(recorder.Requests);
    }

    [Fact]
    public async Task Reauthentication_MultipartContent_IsNotRetried()
    {
        var ct = TestContext.Current.CancellationToken;
        var provider = new StubAuthorizationProvider(new AuthenticationHeaderValue("Bearer", "old-token"));
        var recorder = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var reauthenticator = new StubReauthenticator(provider, result: true, token: "new-token");

        using var authorization = new ApiAuthorizationHandler(provider) { InnerHandler = recorder };
        using var reauthentication = new ApiReauthenticationHandler(reauthenticator)
        {
            InnerHandler = authorization,
        };
        using var http = new HttpClient(reauthentication);

        using var content = new MultipartFormDataContent();
        using var response = await http.SendAsync(
            new HttpRequestMessage(HttpMethod.Post, "http://localhost/upload") { Content = content }, ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, reauthenticator.Calls);
        Assert.Single(recorder.Requests);
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(respond(request));
        }
    }
}
