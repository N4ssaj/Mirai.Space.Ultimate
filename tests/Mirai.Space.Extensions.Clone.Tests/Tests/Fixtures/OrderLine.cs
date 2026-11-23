using Mirai.Space.Extensions.Clone;

namespace Mirai.Space.Extensions.Clone.Tests;

[Cloneable]
public sealed class OrderLine
{
    public string Sku { get; set; } = string.Empty;

    public int Quantity { get; set; }
}
