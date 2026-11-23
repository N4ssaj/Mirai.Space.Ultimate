using Mirai.Space.Extensions.Clone;

namespace Mirai.Space.Extensions.Clone.Tests;

[Cloneable]
public struct Coordinate
{
    public int X { get; set; }

    public int Y { get; set; }

    public Address? Address { get; set; }
}
