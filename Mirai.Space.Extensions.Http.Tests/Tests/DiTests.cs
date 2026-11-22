using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using Mirai.Space.Extensions.Http.Abstractions;
using Mirai.Space.Extensions.Http.Extensions;
using Mirai.Space.Extensions.Http.Json.Extensions;
using Xunit;

namespace Mirai.Space.Extensions.Http.Tests;

public sealed class DiTests
{
    [Fact]
    public async Task AddRequestService_ResolvesTypedClientWithJson()
    {
        var ct = TestContext.Current.CancellationToken;
        using var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"id":1,"name":"from-di"}""", System.Text.Encoding.UTF8, "application/json"),
        });

        var services = new ServiceCollection();
        services.AddRequestService<TestClient>()
            .ConfigurePrimaryHttpMessageHandler(() => handler)
            .UseJson();

        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<TestClient>();

        var model = await client.GetItemAsync(ct);

        Assert.NotNull(model);
        Assert.Equal(1, model.Id);
        Assert.Equal("from-di", model.Name);
    }

    [Fact]
    public async Task AddRequestService_NonSuccess_UsesProblemDetailsFactory()
    {
        var ct = TestContext.Current.CancellationToken;
        using var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent(
                """{"type":"about:blank","title":"Invalid item","status":400,"detail":"id must be positive"}""",
                System.Text.Encoding.UTF8,
                "application/problem+json"),
        });

        var services = new ServiceCollection();
        services.AddProblemDetailsErrorFactory();
        services.AddRequestService<TestClient>()
            .ConfigurePrimaryHttpMessageHandler(() => handler)
            .UseJson();

        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<TestClient>();

        var exception = await Assert.ThrowsAsync<ApiException>(() => client.GetItemAsync(ct));
        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
        var problem = Assert.IsType<ApiProblemDetails>(exception.Problem);
        Assert.Equal("Invalid item", problem.Title);
        Assert.NotNull(exception.ResponseBody);
    }

    [Fact]
    public void AddRequestService_RegistersAuthorizationProvider()
    {
        var services = new ServiceCollection();
        services.AddRequestService<TestClient>()
            .UseJson();

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<IApiAuthorizationProvider>());
        Assert.NotNull(provider.GetService<IApiReauthenticator>());
    }
}
