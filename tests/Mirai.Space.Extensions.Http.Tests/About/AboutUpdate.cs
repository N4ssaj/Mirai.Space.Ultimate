using MessagePack;

namespace Mirai.Space.Extensions.Http.Tests.About;

[MessagePackObject]
public sealed class AboutUpdate
{
    [Key(0)]
    public string Name { get; set; } = string.Empty;

    [Key(1)]
    public string Description { get; set; } = string.Empty;
}
