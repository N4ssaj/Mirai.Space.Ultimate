using Mirai.Space.Extensions.Clone;

namespace Mirai.Space.Extensions.Clone.Tests;

[Cloneable]
public partial record AnnotatedRecord(string Name, Address Address);
