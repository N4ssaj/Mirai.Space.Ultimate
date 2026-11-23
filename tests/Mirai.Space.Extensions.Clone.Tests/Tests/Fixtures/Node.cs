using Mirai.Space.Extensions.Clone;

namespace Mirai.Space.Extensions.Clone.Tests;

[Cloneable]
public sealed class Node
{
    public string Name { get; set; } = string.Empty;

    public Node? Next { get; set; }
}
