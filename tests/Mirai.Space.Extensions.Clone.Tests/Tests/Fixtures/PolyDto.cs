using PolyType;

namespace Mirai.Space.Extensions.Clone.Tests;

[GenerateShape]
public partial class PolyDto
{
    public string Name { get; set; } = string.Empty;

    public Address? Address { get; set; }
}
