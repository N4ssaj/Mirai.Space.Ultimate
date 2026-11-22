namespace Mirai.Space.Extensions.Http.Abstractions;

public interface IRequestSerializer
{
    string ContentType { get; }

    ReadOnlyMemory<byte> Serialize<T>(T value);

    ValueTask<T?> DeserializeAsync<T>(Stream stream, CancellationToken ct = default);

    IAsyncEnumerable<T> DeserializeAsyncEnumerable<T>(
        Stream stream,
        string? contentType,
        CancellationToken ct = default);
}
