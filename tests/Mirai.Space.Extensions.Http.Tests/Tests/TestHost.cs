using Microsoft.Extensions.DependencyInjection;
using Mirai.Space.Extensions.Http.Extensions;

namespace Mirai.Space.Extensions.Http.Tests;

internal sealed class TestHost : IDisposable
{
    private readonly ServiceProvider _provider;

    private TestHost(ServiceProvider provider, TestHttpMessageHandler handler, TestClient client)
    {
        _provider = provider;
        Handler = handler;
        Client = client;
    }

    public TestHttpMessageHandler Handler { get; }

    public TestClient Client { get; }

    public IServiceProvider Services => _provider;

    public static TestHost Create(
        Func<HttpRequestMessage, HttpResponseMessage> respond,
        Action<IHttpClientBuilder>? configure = null,
        Action<IServiceCollection>? configureServices = null)
    {
        var handler = new TestHttpMessageHandler(respond);
        var services = new ServiceCollection();
        configureServices?.Invoke(services);

        var builder = services.AddRequestService<TestClient>()
            // Registers the stub as the primary handler for the named TestClient.
            .ConfigurePrimaryHttpMessageHandler(() => handler);
        configure?.Invoke(builder);

        var provider = services.BuildServiceProvider();
        return new TestHost(provider, handler, provider.GetRequiredService<TestClient>());
    }

    public T GetRequiredService<T>()
        where T : notnull =>
        _provider.GetRequiredService<T>();

    public void Dispose() => _provider.Dispose();
}
