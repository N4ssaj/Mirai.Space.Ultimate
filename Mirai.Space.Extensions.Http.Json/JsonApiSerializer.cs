using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Mirai.Space.Extensions.Http.Abstractions;

namespace Mirai.Space.Extensions.Http.Json;

public sealed class JsonApiSerializer : IApiSerializer
{
    private const string EventStreamContentType = "text/event-stream";

    private readonly JsonSerializerOptions _options;

    public string ContentType => "application/json";

    public JsonApiSerializer()
        : this(new JsonSerializerOptions(JsonSerializerDefaults.Web))
    {
    }

    public JsonApiSerializer(JsonSerializerOptions options)
    {
        // Snapshot so a shared options instance stays mutable for the caller.
        _options = new JsonSerializerOptions(options);
    }

    public JsonApiSerializer(JsonSerializerContext context)
    {
        _options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { TypeInfoResolver = context };
    }

    public ReadOnlyMemory<byte> Serialize<T>(T value) =>
        JsonSerializer.SerializeToUtf8Bytes(value, _options);

    public ValueTask<T?> DeserializeAsync<T>(Stream stream, CancellationToken ct = default) =>
        JsonSerializer.DeserializeAsync<T>(stream, _options, ct);

    public async IAsyncEnumerable<T> DeserializeAsyncEnumerable<T>(
        Stream stream,
        string? contentType,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (string.Equals(contentType, EventStreamContentType, StringComparison.OrdinalIgnoreCase))
        {
            var parser = SseParser.Create<T>(stream, (eventType, data) =>
                JsonSerializer.Deserialize<T>(data, _options)
                ?? throw new JsonException($"SSE event '{eventType}' has a payload that is not a {typeof(T)}."));

            await foreach (var item in parser.EnumerateAsync(ct).ConfigureAwait(false))
            {
                yield return item.Data;
            }

            yield break;
        }

        if (IsNdJson(contentType))
        {
            using var reader = new StreamReader(
                stream,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: false,
                bufferSize: 4096,
                leaveOpen: true);

            while (await reader.ReadLineAsync(ct).ConfigureAwait(false) is { } line)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                yield return JsonSerializer.Deserialize<T>(line, _options)
                    ?? throw new JsonException($"NDJSON line is not a {typeof(T)}.");
            }

            yield break;
        }

        await foreach (var item in JsonSerializer
                           .DeserializeAsyncEnumerable<T>(stream, _options, ct)
                           .WithCancellation(ct)
                           .ConfigureAwait(false))
        {
            if (item is not null)
            {
                yield return item;
            }
        }
    }

    private static bool IsNdJson(string? contentType) =>
        string.Equals(contentType, "application/x-ndjson", StringComparison.OrdinalIgnoreCase)
        || string.Equals(contentType, "application/jsonl", StringComparison.OrdinalIgnoreCase)
        || string.Equals(contentType, "application/x-jsonlines", StringComparison.OrdinalIgnoreCase);
}
