using System.Net;
using System.Net.Http.Headers;
using Mirai.Space.Extensions.Http.Abstractions;

namespace Mirai.Space.Extensions.Http.Tests;

internal sealed class TestErrorFactory : IApiErrorFactory
{
    public ValueTask<ApiException> CreateAsync(HttpResponseMessage response, CancellationToken ct = default)
    {
        var request = response.RequestMessage;
        return ValueTask.FromResult(new ApiException(
            $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}",
            response.StatusCode,
            request?.Method,
            request?.RequestUri));
    }
}

internal sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken) =>
        Task.FromResult(respond(request));
}

internal sealed class TestClient(IRequestService requests)
{
    public Task<TestModel?> GetItemAsync(CancellationToken ct) =>
        requests.GetAsync<TestModel>("http://localhost/items/1", ct);
}

public sealed record TestModel(int Id, string Name);

internal sealed class StubAuthorizationProvider(AuthenticationHeaderValue? token) : IApiAuthorizationProvider
{
    public AuthenticationHeaderValue? Token { get; set; } = token;

    public ValueTask<AuthenticationHeaderValue?> GetAuthorizationAsync(CancellationToken ct = default) =>
        ValueTask.FromResult(Token);
}

internal sealed class StubReauthenticator(
    StubAuthorizationProvider provider,
    bool result,
    string? token = null) : IApiReauthenticator
{
    public int Calls { get; private set; }

    public Task<bool> TryReauthenticateAsync(CancellationToken ct = default)
    {
        Calls++;
        if (result && token is not null)
        {
            provider.Token = new AuthenticationHeaderValue("Bearer", token);
        }

        return Task.FromResult(result);
    }
}
