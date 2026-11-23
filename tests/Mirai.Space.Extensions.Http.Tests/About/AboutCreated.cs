using MessagePack;

namespace Mirai.Space.Extensions.Http.Tests.About;

[MessagePackObject]
public sealed class AboutCreated
{
    [Key(0)]
    public Guid Id { get; set; }

    [Key(1)]
    public string Name { get; set; } = string.Empty;
}
