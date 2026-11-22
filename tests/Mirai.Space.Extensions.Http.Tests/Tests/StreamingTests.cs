using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Mirai.Space.Extensions.Http.Abstractions;
using Mirai.Space.Extensions.Http.Json;
using Mirai.Space.Extensions.Http.MessagePack;
using MessagePack;
using Xunit;

namespace Mirai.Space.Extensions.Http.Tests;

public sealed class StreamingTests
{
    [Fact]
    public async Task GetAsyncEnumerable_JsonArray_ReadsAllItems()
    {
        var handler = JsonHandler("""[{"id":1,"name":"a"},{"id":2,"name":"b"}]""", "application/json");
        var service = CreateService(handler, new JsonApiSerializer());

        var items = new List<TestModel>();
        await foreach (var item in service.GetAsyncEnumerable<TestModel>(
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
        var service = CreateService(handler, new JsonApiSerializer());

        var items = new List<TestModel>();
        await foreach (var item in service.GetAsyncEnumerable<TestModel>(
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
        var service = CreateService(handler, new JsonApiSerializer());

        var items = new List<TestModel>();
        await foreach (var item in service.GetAsyncEnumerable<TestModel>(
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
        var service = CreateService(handler, new JsonApiSerializer());

        await Assert.ThrowsAsync<JsonException>(async () =>
        {
            await foreach (var _ in service.GetAsyncEnumerable<TestModel>(
                               "http://localhost/events", TestContext.Current.CancellationToken))
            {
            }
        });
    }

    [Fact]
    public async Task GetAsyncEnumerable_MsgPack_ReadsSequenceOfValues()
    {
        var serializer = new MessagePackApiSerializer();
        var first = serializer.Serialize(new MsgPackModel { Id = 1, Name = "a" });
        var second = serializer.Serialize(new MsgPackModel { Id = 2, Name = "b" });
        var body = first.ToArray().Concat(second.ToArray()).ToArray();
        var handler = MsgPackHandler(body);
        var service = CreateService(handler, serializer);

        var items = new List<MsgPackModel>();
        await foreach (var item in service.GetAsyncEnumerable<MsgPackModel>(
                           "http://localhost/items", TestContext.Current.CancellationToken))
        {
            items.Add(item);
        }

        Assert.Equal(new[] { 1, 2 }, items.Select(x => x.Id));
    }

    [Fact]
    public async Task GetAsyncEnumerable_MsgPack_TopLevelArrayOfObjects_ThrowsSerializationException()
    {
        var serializer = new MessagePackApiSerializer();
        var first = serializer.Serialize(new MsgPackModel { Id = 1, Name = "a" }).ToArray();
        var second = serializer.Serialize(new MsgPackModel { Id = 2, Name = "b" }).ToArray();
        var body = new byte[] { 0x92 }.Concat(first).Concat(second).ToArray();
        var handler = MsgPackHandler(body);
        var service = CreateService(handler, serializer);

        await Assert.ThrowsAsync<MessagePackSerializationException>(async () =>
        {
            await foreach (var _ in service.GetAsyncEnumerable<MsgPackModel>(
                               "http://localhost/items", TestContext.Current.CancellationToken))
            {
            }
        });
    }

    [Fact]
    public async Task MessagePackSerializer_RoundTrips()
    {
        var serializer = new MessagePackApiSerializer();
        var bytes = serializer.Serialize(new MsgPackModel { Id = 7, Name = "x" });
        using var stream = new MemoryStream(bytes.ToArray());

        var result = await serializer.DeserializeAsync<MsgPackModel>(
            stream, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(7, result.Id);
        Assert.Equal("x", result.Name);
    }

    private static RequestService CreateService(HttpMessageHandler handler, IApiSerializer serializer) =>
        new(new HttpClient(handler), serializer, new TestErrorFactory());

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

[MessagePackObject]
public sealed class MsgPackModel
{
    [Key(0)]
    public int Id { get; init; }

    [Key(1)]
    public string Name { get; set; } = string.Empty;
}
