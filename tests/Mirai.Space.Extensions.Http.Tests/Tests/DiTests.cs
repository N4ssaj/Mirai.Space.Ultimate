using System.Net;
using System.Text;
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
            Content = new StringContent("""{"id":1,"name":"from-di"}""", Encoding.UTF8, "application/json"),
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
    public async Task AddRequestService_NonSuccess_UsesProblemDetailsHandler()
    {
        var ct = TestContext.Current.CancellationToken;
        using var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent(
                """{"type":"about:blank","title":"Invalid item","status":400,"detail":"id must be positive"}""",
                Encoding.UTF8,
                "application/problem+json"),
        });

        var services = new ServiceCollection();
        services.AddProblemDetailsErrorHandler();
        services.AddRequestService<TestClient>()
            .ConfigurePrimaryHttpMessageHandler(() => handler)
            .UseJson();

        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<TestClient>();

        var exception = await Assert.ThrowsAsync<ApiException>(() => client.GetItemAsync(ct));
        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
        var problem = Assert.IsType<RequestProblemDetails>(exception.Problem);
        Assert.Equal("Invalid item", problem.Title);
    }
}
