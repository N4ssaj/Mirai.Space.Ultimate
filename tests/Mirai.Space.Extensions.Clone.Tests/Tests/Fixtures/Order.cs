using Mirai.Space.Extensions.Clone;

namespace Mirai.Space.Extensions.Clone.Tests;

[Cloneable]
public sealed class Order
{
    public string Number { get; set; } = string.Empty;

    public Address? ShippingAddress { get; set; }

    public List<OrderLine> Lines { get; set; } = [];

    public Dictionary<string, string> Metadata { get; set; } = [];
}
