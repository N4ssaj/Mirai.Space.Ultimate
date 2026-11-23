using System.Runtime.CompilerServices;
using MessagePack;
using Mirai.Space.Extensions.Http.Abstractions;

namespace Mirai.Space.Extensions.Http.MessagePack;

internal sealed class MessagePackRequestSerializer : IRequestSerializer
{
    private readonly MessagePackSerializerOptions _options;

    public string ContentType => "application/x-msgpack";

    public MessagePackRequestSerializer()
        : this(MessagePackSerializerOptions.Standard)
    {
    }

    public MessagePackRequestSerializer(MessagePackSerializerOptions options)
    {
        _options = options;
    }

    public MessagePackRequestSerializer(IFormatterResolver resolver)
        : this(MessagePackSerializerOptions.Standard.WithResolver(resolver))
    {
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
