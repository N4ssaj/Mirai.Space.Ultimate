using Microsoft.Extensions.DependencyInjection;
using Mirai.Space.Extensions.Http.Abstractions;
using Mirai.Space.Extensions.Http.Extensions;

namespace Mirai.Space.Extensions.Http.Tests;

internal static class TestHost
{
    public static (ServiceProvider Provider, TestClient Client) Create(
        HttpMessageHandler handler,
        Action<IHttpClientBuilder> configure,
        Action<IServiceCollection>? configureServices = null)
    {
        var services = new ServiceCollection();
        configureServices?.Invoke(services);

        var builder = services.AddRequestService<TestClient>()
            .ConfigurePrimaryHttpMessageHandler(() => handler);
        configure(builder);

        var provider = services.BuildServiceProvider();
        return (provider, provider.GetRequiredService<TestClient>());
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
    public IRequestService Requests { get; } = requests;

    public Task<TestModel?> GetItemAsync(CancellationToken ct) =>
        Requests.GetAsync<TestModel>("http://localhost/items/1", ct);
}
