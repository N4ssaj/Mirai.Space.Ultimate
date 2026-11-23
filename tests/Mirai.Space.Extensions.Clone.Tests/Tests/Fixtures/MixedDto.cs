using PolyType;

namespace Mirai.Space.Extensions.Clone.Tests;

[GenerateShape]
public partial class MixedDto
{
    public Address Mutable { get; set; } = new();

    public Address Immutable { get; init; } = new();
}
