using System.Net;
using System.Net.Http;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Mirai.Space.Extensions.Http.Abstractions;
using Mirai.Space.Extensions.Http.Extensions;
using Mirai.Space.Extensions.Http.MessagePack.Extensions;
using Xunit;

namespace Mirai.Space.Extensions.Http.Tests;

public sealed class RequestServiceDiTests
{
    private const string ResourceUrl = "http://localhost/items";

    [Fact]
    public void AddRequestService_RegistersTypedClientAsTransient()
    {
        using var host = TestHost.Create(_ => TestResponses.Empty());

        Assert.NotSame(
            host.GetRequiredService<TestClient>(),
            host.GetRequiredService<TestClient>());
    }

    [Fact]
    public void AddRequestService_RegistersRequestServiceAsTransient()
    {
        using var host = TestHost.Create(_ => TestResponses.Empty());
        var name = typeof(TestClient).FullName!;
        var services = host.Services;

        Assert.NotSame(
            services.GetRequiredKeyedService<IRequestService>(name),
            services.GetRequiredKeyedService<IRequestService>(name));
    }

    [Fact]
    public async Task AddRequestService_WithoutConfigureJson_UsesJsonByDefault()
    {
        using var host = TestHost.Create(_ => TestResponses.Json("""{"id":1,"name":"default"}"""));

        var result = await host.Client.Requests.GetAsync<TestModel>(
            ResourceUrl, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal("default", result.Name);
    }

    [Fact]
    public async Task ConfigureJson_WithOptions_ConfiguresSerializer()
    {
        using var host = TestHost.Create(
            _ => TestResponses.Empty(),
            builder => builder.ConfigureJson(options => options.PropertyNamingPolicy = null));

        await host.Client.Requests.PostAsync(
            ResourceUrl, new TestModel(1, "value"), TestContext.Current.CancellationToken);

        var body = Encoding.UTF8.GetString(host.Handler.Requests[^1].Content!);
        Assert.Contains("\"Name\":\"value\"", body);
    }

    [Fact]
    public void ConfigureJson_SetsAcceptHeader()
    {
        using var host = TestHost.Create(_ => TestResponses.Empty());

        var client = host.GetRequiredService<IHttpClientFactory>()
            .CreateClient(typeof(TestClient).FullName!);

        var accept = Assert.Single(client.DefaultRequestHeaders.Accept);
        Assert.Equal("application/json", accept.MediaType);
    }

    [Fact]
    public async Task UseMessagePack_OverridesSerializerForOneClient()
    {
        using var host = TestHost.Create(
            _ => TestResponses.Empty(),
            builder => builder.UseMessagePack());

        await host.Client.Requests.PostAsync(
            ResourceUrl, new MessagePackModel { Id = 1, Name = "value" },
            TestContext.Current.CancellationToken);

        Assert.Equal("application/x-msgpack", host.Handler.Requests[^1].ContentType);
        Assert.Equal(
            "application/x-msgpack",
            host.Services.GetRequiredKeyedService<IRequestSerializer>(
                typeof(TestClient).FullName!).ContentType);
    }

    [Fact]
    public void UseMessagePack_SetsAcceptHeader()
    {
        using var host = TestHost.Create(
            _ => TestResponses.Empty(),
            builder => builder.UseMessagePack());

        var client = host.GetRequiredService<IHttpClientFactory>()
            .CreateClient(typeof(TestClient).FullName!);

        var accept = Assert.Single(client.DefaultRequestHeaders.Accept);
        Assert.Equal("application/x-msgpack", accept.MediaType);
    }

    [Fact]
    public void UseSerializer_Instance_SetsAcceptHeader()
    {
        using var host = TestHost.Create(
            _ => TestResponses.Empty(),
            builder => builder.UseSerializer(new CustomSerializer()));

        var client = host.GetRequiredService<IHttpClientFactory>()
            .CreateClient(typeof(TestClient).FullName!);

        var accept = Assert.Single(client.DefaultRequestHeaders.Accept);
        Assert.Equal(CustomSerializer.MediaType, accept.MediaType);
    }

    [Fact]
    public async Task UseSerializer_Instance_OverridesBodySerialization()
    {
        using var host = TestHost.Create(
            _ => TestResponses.Empty(),
            builder => builder.UseSerializer(new CustomSerializer()));

        await host.Client.Requests.PostAsync(
            ResourceUrl, new TestModel(1, "value"), TestContext.Current.CancellationToken);

        var request = host.Handler.Requests[^1];
        Assert.Equal(CustomSerializer.MediaType, request.ContentType);
        Assert.Equal("custom", Encoding.UTF8.GetString(request.Content!));
    }

    [Fact]
    public async Task TwoClients_UseDifferentSerializers()
    {
        var handler = new TestHttpMessageHandler(_ =>
            TestResponses.Empty(HttpStatusCode.NoContent));
        var services = new ServiceCollection();
        services.AddRequestService<TestClient>()
            .ConfigurePrimaryHttpMessageHandler(() => handler);
        services.AddRequestService<SecondTestClient>()
            .ConfigurePrimaryHttpMessageHandler(() => handler)
            .UseMessagePack();

        using var provider = services.BuildServiceProvider();
        var jsonClient = provider.GetRequiredService<TestClient>();
        var msgPackClient = provider.GetRequiredService<SecondTestClient>();

        await jsonClient.Requests.PostAsync(
            ResourceUrl, new TestModel(1, "json"), TestContext.Current.CancellationToken);
        await msgPackClient.Requests.PostAsync(
            ResourceUrl, new MessagePackModel { Id = 1, Name = "msgpack" },
            TestContext.Current.CancellationToken);

        Assert.Equal("application/json", handler.Requests[0].ContentType);
        Assert.Equal("application/x-msgpack", handler.Requests[1].ContentType);
    }

    [Fact]
    public void AddRequestService_CalledTwice_RegistersSingleDescriptorPerService()
    {
        var services = new ServiceCollection();

        services.AddRequestService<TestClient>();
        services.AddRequestService<TestClient>();

        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(TestClient));
        Assert.Single(services, descriptor =>
            descriptor.ServiceType == typeof(IRequestService) &&
            Equals(descriptor.ServiceKey, typeof(TestClient).FullName));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IRequestSerializer));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IRequestErrorHandler));
    }

    [Fact]
    public void RequestServiceOptions_DefaultToHeadersRead()
    {
        var options = new RequestServiceOptions();

        Assert.Equal(HttpCompletionOption.ResponseHeadersRead, options.CompletionOption);
    }

    [Fact]
    public void ConfigureRequestService_SetsCompletionOption()
    {
        using var host = TestHost.Create(
            _ => TestResponses.Empty(),
            builder => builder.ConfigureRequestService(
                options => options.CompletionOption = HttpCompletionOption.ResponseContentRead));

        var options = host.GetRequiredService<IOptionsMonitor<RequestServiceOptions>>()
            .Get(typeof(TestClient).FullName!);

        Assert.Equal(HttpCompletionOption.ResponseContentRead, options.CompletionOption);
    }

    private sealed class CustomSerializer : IRequestSerializer
    {
        public const string MediaType = "application/x-custom";

        public string ContentType => MediaType;

        public ReadOnlyMemory<byte> Serialize<T>(T value) =>
            Encoding.UTF8.GetBytes("custom");

        public ValueTask<T?> DeserializeAsync<T>(Stream stream, CancellationToken ct = default) =>
            ValueTask.FromResult<T?>(default);

        public async IAsyncEnumerable<T> DeserializeAsyncEnumerable<T>(
            Stream stream,
            string? contentType,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            await Task.CompletedTask;
            yield break;
        }
    }

    private sealed class SecondTestClient(IRequestService requests)
    {
        public IRequestService Requests { get; } = requests;
    }
}
