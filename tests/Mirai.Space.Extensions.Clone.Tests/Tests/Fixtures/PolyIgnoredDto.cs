using Mirai.Space.Extensions.Clone;
using PolyType;

namespace Mirai.Space.Extensions.Clone.Tests;

[GenerateShape]
public partial class PolyIgnoredDto
{
    public Address Address { get; set; } = new();

    [CloneIgnore]
    public Address Shared { get; set; } = new();
}
