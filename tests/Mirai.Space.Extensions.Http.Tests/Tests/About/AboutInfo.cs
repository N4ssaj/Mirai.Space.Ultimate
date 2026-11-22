using MessagePack;

namespace Mirai.Space.Extensions.Http.Tests.About;

[MessagePackObject]
public sealed class AboutInfo
{
    [Key(0)]
    public string Name { get; set; } = string.Empty;

    [Key(1)]
    public string Version { get; set; } = string.Empty;

    [Key(2)]
    public string Description { get; set; } = string.Empty;
}
