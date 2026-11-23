using Mirai.Space.Extensions.Clone;

namespace Mirai.Space.Extensions.Clone.Tests;

[Cloneable(GenerateCloneMethod = true)]
public partial class GeneratedMethodDto
{
    public string Name { get; set; } = string.Empty;

    public GeneratedMethodDto? Child { get; set; }
}
