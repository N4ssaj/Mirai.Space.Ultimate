using System.Runtime.CompilerServices;
using MessagePack;
using Mirai.Space.Extensions.Http.Abstractions;

namespace Mirai.Space.Extensions.Http.MessagePack;

public sealed class MessagePackApiSerializer : IApiSerializer
{
    private const string MsgPackContentType = "application/x-msgpack";

    private readonly MessagePackSerializerOptions _options;

    public string ContentType => MsgPackContentType;

    public MessagePackApiSerializer()
        : this(MessagePackSerializerOptions.Standard)
    {
    }

    public MessagePackApiSerializer(MessagePackSerializerOptions options)
    {
        _options = options;
    }

    public ReadOnlyMemory<byte> Serialize<T>(T value) =>
        MessagePackSerializer.Serialize(value, _options);

    public async ValueTask<T?> DeserializeAsync<T>(Stream stream, CancellationToken ct = default) =>
        await MessagePackSerializer.DeserializeAsync<T>(stream, _options, ct).ConfigureAwait(false);

    public async IAsyncEnumerable<T> DeserializeAsyncEnumerable<T>(
        Stream stream,
        string? contentType,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        using var reader = new MessagePackStreamReader(stream, leaveOpen: true);
        while (await reader.ReadAsync(ct).ConfigureAwait(false) is { } message)
        {
            if (message.Length == 0)
            {
                continue;
            }

            yield return MessagePackSerializer.Deserialize<T>(message, _options, ct);
        }
    }
}
