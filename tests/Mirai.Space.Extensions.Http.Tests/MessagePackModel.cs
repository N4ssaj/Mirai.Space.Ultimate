using MessagePack;

namespace Mirai.Space.Extensions.Http.Tests;

[MessagePackObject]
public sealed class MessagePackModel
{
    [Key(0)]
    public int Id { get; init; }

    [Key(1)]
    public string Name { get; set; } = string.Empty;
}
