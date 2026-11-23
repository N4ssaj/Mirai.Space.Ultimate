using System.ComponentModel;

namespace Mirai.Space.Extensions.ComponentModel.Tests;

[DefaultProperty("Missing")]
public sealed class BrokenDefaultModel
{
    public string Name { get; set; } = string.Empty;
}
