using Mirai.Space.Extensions.Clone;

namespace Mirai.Space.Extensions.Clone.Tests;

[Cloneable]
public sealed class Address
{
    public string City { get; set; } = string.Empty;
}
