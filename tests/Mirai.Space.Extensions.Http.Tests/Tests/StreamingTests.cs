using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Mirai.Space.Extensions.Http.Abstractions;
using Mirai.Space.Extensions.Http.Json.Extensions;
using Mirai.Space.Extensions.Http.MessagePack.Extensions;
using Xunit;

namespace Mirai.Space.Extensions.Http.Tests;

public sealed class StreamingTests
{
    [Fact]
    public async Task GetAsyncEnumerable_JsonArray_ReadsAllItems()
    {
        var handler = JsonHandler("""[{"id":1,"name":"a"},{"id":2,"name":"b"}]""", "application/json");
        var (provider, client) = TestHost.Create(handler, b => b.UseJson());
        using var _ = provider;

        var items = new List<TestModel>();
        await foreach (var item in client.Requests.GetAsyncEnumerable<TestModel>(
                           "http://localhost/items", TestContext.Current.CancellationToken))
        {
            items.Add(item);
        }

        Assert.Equal(new[] { 1, 2 }, items.Select(x => x.Id));
    }

    [Fact]
    public async Task GetAsyncEnumerable_NdJson_ReadsEachLine()
    {
        const string body = """
                            {"id":1,"name":"a"}
                            {"id":2,"name":"b"}
                            """;
        var handler = JsonHandler(body, "application/x-ndjson");
        var (provider, client) = TestHost.Create(handler, b => b.UseJson());
        using var _ = provider;

        var items = new List<TestModel>();
        await foreach (var item in client.Requests.GetAsyncEnumerable<TestModel>(
                           "http://localhost/items", TestContext.Current.CancellationToken))
        {
            items.Add(item);
        }

        Assert.Equal(new[] { 1, 2 }, items.Select(x => x.Id));
    }

    [Fact]
    public async Task GetAsyncEnumerable_Sse_ReadsEachEventData()
    {
        const string body =
            "event: item\ndata: {\"id\":1,\"name\":\"a\"}\n\nevent: item\ndata: {\"id\":2,\"name\":\"b\"}\n\n";
        var handler = JsonHandler(body, "text/event-stream");
        var (provider, client) = TestHost.Create(handler, b => b.UseJson());
        using var _ = provider;

        var items = new List<TestModel>();
        await foreach (var item in client.Requests.GetAsyncEnumerable<TestModel>(
                           "http://localhost/events", TestContext.Current.CancellationToken))
        {
            items.Add(item);
        }

        Assert.Equal(new[] { 1, 2 }, items.Select(x => x.Id));
    }

    [Fact]
    public async Task GetAsyncEnumerable_SseNullPayload_ThrowsJsonException()
    {
        var handler = JsonHandler("event: item\ndata: null\n\n", "text/event-stream");
        var (provider, client) = TestHost.Create(handler, b => b.UseJson());
        using var _ = provider;

        await Assert.ThrowsAsync<JsonException>(async () =>
        {
            await foreach (var _ in client.Requests.GetAsyncEnumerable<TestModel>(
                               "http://localhost/events", TestContext.Current.CancellationToken))
            {
            }
        });
    }

    [Fact]
    public async Task GetAsyncEnumerable_MsgPack_ReadsSequenceOfValues()
    {
        var first = MessagePackSerializer.Serialize(new MsgPackModel { Id = 1, Name = "a" }, cancellationToken: TestContext.Current.CancellationToken);
        var second = MessagePackSerializer.Serialize(new MsgPackModel { Id = 2, Name = "b" }, cancellationToken: TestContext.Current.CancellationToken);
        var handler = MsgPackHandler(first.Concat(second).ToArray());
        var (provider, client) = TestHost.Create(handler, b => b.UseMessagePack());
        using var _ = provider;

        var items = new List<MsgPackModel>();
        await foreach (var item in client.Requests.GetAsyncEnumerable<MsgPackModel>(
                           "http://localhost/items", TestContext.Current.CancellationToken))
        {
            items.Add(item);
        }

        Assert.Equal(new[] { 1, 2 }, items.Select(x => x.Id));
    }

    [Fact]
    public async Task GetAsyncEnumerable_MsgPack_TopLevelArrayOfObjects_ThrowsSerializationException()
    {
        var first = MessagePackSerializer.Serialize(new MsgPackModel { Id = 1, Name = "a" }, cancellationToken: TestContext.Current.CancellationToken);
        var second = MessagePackSerializer.Serialize(new MsgPackModel { Id = 2, Name = "b" }, cancellationToken: TestContext.Current.CancellationToken);
        var body = new byte[] { 0x92 }.Concat(first).Concat(second).ToArray();
        var handler = MsgPackHandler(body);
        var (provider, client) = TestHost.Create(handler, b => b.UseMessagePack());
        using var _ = provider;

        await Assert.ThrowsAsync<MessagePackSerializationException>(async () =>
        {
            await foreach (var _ in client.Requests.GetAsyncEnumerable<MsgPackModel>(
                               "http://localhost/items", TestContext.Current.CancellationToken))
            {
            }
        });
    }

    [Fact]
    public async Task GetAsyncEnumerable_MsgPack_Sse_ThrowsNotSupported()
    {
        var handler = JsonHandler("data: 1\n\n", "text/event-stream");
        var (provider, client) = TestHost.Create(handler, b => b.UseMessagePack());
        using var _ = provider;

        await Assert.ThrowsAsync<NotSupportedException>(async () =>
        {
            await foreach (var _ in client.Requests.GetAsyncEnumerable<MsgPackModel>(
                               "http://localhost/events", TestContext.Current.CancellationToken))
            {
            }
        });
    }

    [Fact]
    public async Task MessagePackSerializer_RoundTrips()
    {
        var services = new ServiceCollection();
        services.AddMessagePackSerializer();
        using var provider = services.BuildServiceProvider();
        var serializer = provider.GetRequiredService<IRequestSerializer>();

        var bytes = serializer.Serialize(new MsgPackModel { Id = 7, Name = "x" });
        using var stream = new MemoryStream(bytes.ToArray());

        var result = await serializer.DeserializeAsync<MsgPackModel>(
            stream, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(7, result.Id);
        Assert.Equal("x", result.Name);
    }

    [Fact]
    public async Task GetStreamAsync_CopiesBodyIntoDestination()
    {
        var handler = JsonHandler("chunk-1-chunk-2", "text/plain");
        var (provider, client) = TestHost.Create(handler, b => b.UseJson());
        using var _ = provider;
        using var destination = new MemoryStream();

        var response = await client.Requests.GetStreamAsync(
            "http://localhost/stream", destination, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("chunk-1-chunk-2", Encoding.UTF8.GetString(destination.ToArray()));
    }

    private static StubHandler JsonHandler(string body, string mediaType) =>
        new(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, mediaType),
        });

    private static StubHandler MsgPackHandler(byte[] body) =>
        new(_ =>
        {
            var content = new ByteArrayContent(body);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/x-msgpack");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        });
}
