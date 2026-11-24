using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Mirai.Space.Extensions.Http.Abstractions;

namespace Mirai.Space.Extensions.Http.Json;

internal sealed class JsonRequestSerializer : IRequestSerializer
{
    private const string EventStreamContentType = "text/event-stream";

    private readonly JsonSerializerOptions _options;

    public string ContentType => "application/json";

    public JsonRequestSerializer(JsonSerializerOptions options)
    {
        _options = options;
    }

    public ReadOnlyMemory<byte> Serialize<T>(T value) =>
        JsonSerializer.SerializeToUtf8Bytes(value, _options);

    public ValueTask<T?> DeserializeAsync<T>(Stream stream, CancellationToken ct = default) =>
        JsonSerializer.DeserializeAsync<T>(stream, _options, ct);

    public IAsyncEnumerable<T> DeserializeAsyncEnumerable<T>(
        Stream stream,
        string? mediaType,
        CancellationToken ct = default) =>
        GetFormat(mediaType) switch
        {
            StreamFormat.ServerSentEvents => ReadServerSentEventsAsync<T>(stream, ct),
            StreamFormat.NdJson => ReadNdJsonAsync<T>(stream, ct),
            _ => ReadJsonArrayAsync<T>(stream, ct),
        };

    private async IAsyncEnumerable<T> ReadServerSentEventsAsync<T>(
        Stream stream,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var parser = SseParser.Create<T>(stream, (eventType, data) =>
            DeserializeRequired<T>(
                data,
                $"SSE event '{eventType}' has a payload that is not a {typeof(T)}."));

        await foreach (var item in parser.EnumerateAsync(ct).ConfigureAwait(false))
        {
            yield return item.Data;
        }
    }

    private async IAsyncEnumerable<T> ReadNdJsonAsync<T>(
        Stream stream,
        [EnumeratorCancellation] CancellationToken ct)
    {
        using var reader = new StreamReader(
            stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            bufferSize: 4096,
            leaveOpen: true);

        while (await reader.ReadLineAsync(ct).ConfigureAwait(false) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var item = JsonSerializer.Deserialize<T>(line, _options);
            if (item is null)
            {
                continue;
            }

            yield return item;
        }
    }

    private async IAsyncEnumerable<T> ReadJsonArrayAsync<T>(
        Stream stream,
        [EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var item in JsonSerializer
                           .DeserializeAsyncEnumerable<T>(stream, _options, ct)
                           .WithCancellation(ct)
                           .ConfigureAwait(false))
        {
            if (item is null)
            {
                continue;
            }

            yield return item;
        }
    }

    private T DeserializeRequired<T>(ReadOnlySpan<byte> utf8Json, string message)
    {
        var value = JsonSerializer.Deserialize<T>(utf8Json, _options);
        if (value is null)
        {
            ThrowHelper.ThrowJsonException(message);
        }

        return value;
    }

    private static StreamFormat GetFormat(string? mediaType)
    {
        if (string.Equals(mediaType, EventStreamContentType, StringComparison.OrdinalIgnoreCase))
        {
            return StreamFormat.ServerSentEvents;
        }

        if (IsNdJson(mediaType))
        {
            return StreamFormat.NdJson;
        }

        return StreamFormat.JsonArray;
    }

    private static bool IsNdJson(string? mediaType) =>
        string.Equals(mediaType, "application/x-ndjson", StringComparison.OrdinalIgnoreCase)
        || string.Equals(mediaType, "application/jsonl", StringComparison.OrdinalIgnoreCase)
        || string.Equals(mediaType, "application/x-jsonlines", StringComparison.OrdinalIgnoreCase);

    private enum StreamFormat
    {
        JsonArray,
        NdJson,
        ServerSentEvents,
    }
}
