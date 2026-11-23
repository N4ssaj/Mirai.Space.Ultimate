using System.Text.Json;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Mirai.Space.Extensions.Http.Abstractions;
using Mirai.Space.Extensions.Http.Extensions;
using Mirai.Space.Extensions.Http.MessagePack.Extensions;
using Xunit;

namespace Mirai.Space.Extensions.Http.Tests;

public sealed class RequestServiceStreamingTest
{
    private const string StreamUrl = "http://localhost/stream";

    [Fact]
    public async Task GetAsyncEnumerable_JsonArray_ReadsAllItems()
    {
        using var host = TestHost.Create(_ =>
            TestResponses.Json("""[{"id":1,"name":"a"},{"id":2,"name":"b"}]"""));

        var items = await ReadAllAsync(host.Client.Requests.GetAsyncEnumerable<TestModel>(
            StreamUrl, TestContext.Current.CancellationToken));

        Assert.Equal(new[] { 1, 2 }, items.Select(item => item.Id));
    }

    [Fact]
    public async Task GetAsyncEnumerable_JsonLines_ReadsEachLine()
    {
        const string body = """
                            {"id":1,"name":"a"}
                            {"id":2,"name":"b"}
                            """;
        using var host = TestHost.Create(_ => TestResponses.Text(body, "application/x-ndjson"));

        var items = await ReadAllAsync(host.Client.Requests.GetAsyncEnumerable<TestModel>(
            StreamUrl, TestContext.Current.CancellationToken));

        Assert.Equal(new[] { 1, 2 }, items.Select(item => item.Id));
    }

    [Fact]
    public async Task GetAsyncEnumerable_ServerSentEvents_ReadsEachEvent()
    {
        const string body =
            "event: item\ndata: {\"id\":1,\"name\":\"a\"}\n\nevent: item\ndata: {\"id\":2,\"name\":\"b\"}\n\n";
        using var host = TestHost.Create(_ => TestResponses.Text(body, "text/event-stream"));

        var items = await ReadAllAsync(host.Client.Requests.GetAsyncEnumerable<TestModel>(
            StreamUrl, TestContext.Current.CancellationToken));

        Assert.Equal(new[] { 1, 2 }, items.Select(item => item.Id));
    }

    [Fact]
    public async Task GetAsyncEnumerable_ServerSentEventsWithNullPayload_ThrowsJsonException()
    {
        using var host = TestHost.Create(_ =>
            TestResponses.Text("event: item\ndata: null\n\n", "text/event-stream"));

        await Assert.ThrowsAsync<JsonException>(async () =>
            await ReadAllAsync(host.Client.Requests.GetAsyncEnumerable<TestModel>(
                StreamUrl, TestContext.Current.CancellationToken)));
    }

    [Fact]
    public async Task GetAsyncEnumerable_MessagePack_ReadsSequenceOfValues()
    {
        var body = SerializeMsgPack(
            new MsgPackModel { Id = 1, Name = "a" },
            new MsgPackModel { Id = 2, Name = "b" });
        using var host = TestHost.Create(_ => TestResponses.Bytes(body, "application/x-msgpack"),
            builder => builder.UseMessagePack());

        var items = await ReadAllAsync(host.Client.Requests.GetAsyncEnumerable<MsgPackModel>(
            StreamUrl, TestContext.Current.CancellationToken));

        Assert.Equal(new[] { 1, 2 }, items.Select(item => item.Id));
    }

    [Fact]
    public async Task GetAsyncEnumerable_MessagePackTopLevelArray_ThrowsSerializationException()
    {
        var body = new byte[] { 0x92 }
            .Concat(SerializeMsgPack(new MsgPackModel { Id = 1, Name = "a" }))
            .Concat(SerializeMsgPack(new MsgPackModel { Id = 2, Name = "b" }))
            .ToArray();
        using var host = TestHost.Create(_ => TestResponses.Bytes(body, "application/x-msgpack"),
            builder => builder.UseMessagePack());

        await Assert.ThrowsAsync<MessagePackSerializationException>(async () =>
            await ReadAllAsync(host.Client.Requests.GetAsyncEnumerable<MsgPackModel>(
                StreamUrl, TestContext.Current.CancellationToken)));
    }

    [Fact]
    public async Task GetAsyncEnumerable_MessagePackWithSsePayload_ThrowsSerializationException()
    {
        using var host = TestHost.Create(_ => TestResponses.Text("data: 1\n\n", "text/event-stream"),
            builder => builder.UseMessagePack());

        await Assert.ThrowsAsync<MessagePackSerializationException>(async () =>
            await ReadAllAsync(host.Client.Requests.GetAsyncEnumerable<MsgPackModel>(
                StreamUrl, TestContext.Current.CancellationToken)));
    }

    [Fact]
    public async Task DownloadAsync_ResponseHeadersRead_DoesNotBufferDestination()
    {
        using var host = TestHost.Create(_ => TestResponses.Text("abc", "text/plain"));
        using var destination = new MemoryStream();

        await host.Client.Requests.DownloadAsync(
            StreamUrl, destination, TestContext.Current.CancellationToken);

        Assert.Equal(3, destination.Length);
    }

    [Fact]
    public async Task MessagePackSerializer_RoundTripsThroughKeyedRegistration()
    {
        var services = new ServiceCollection();
        services.AddRequestService<TestClient>().UseMessagePack();
        using var provider = services.BuildServiceProvider();
        var serializer = provider.GetRequiredKeyedService<IRequestSerializer>(
            typeof(TestClient).FullName!);

        var bytes = serializer.Serialize(new MsgPackModel { Id = 7, Name = "x" });
        using var stream = new MemoryStream(bytes.ToArray());

        var result = await serializer.DeserializeAsync<MsgPackModel>(
            stream, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(7, result.Id);
        Assert.Equal("x", result.Name);
    }

    private static async Task<List<T>> ReadAllAsync<T>(IAsyncEnumerable<T> source)
    {
        var items = new List<T>();
        await foreach (var item in source)
        {
            items.Add(item);
        }

        return items;
    }

    private static byte[] SerializeMsgPack(params MsgPackModel[] values) =>
        values
            .Select(value => MessagePackSerializer.Serialize(
                value, cancellationToken: TestContext.Current.CancellationToken))
            .SelectMany(bytes => bytes)
            .ToArray();
}
