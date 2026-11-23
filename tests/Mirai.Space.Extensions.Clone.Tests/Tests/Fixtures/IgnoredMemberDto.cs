using Mirai.Space.Extensions.Clone;

namespace Mirai.Space.Extensions.Clone.Tests;

[Cloneable]
public sealed class IgnoredMemberDto
{
    public string Name { get; set; } = string.Empty;

    [CloneIgnore]
    public Address Shared { get; set; } = new();
}
