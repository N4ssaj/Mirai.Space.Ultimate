namespace Mirai.Space.Extensions.Clone;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class CloneableAttribute : Attribute
{
    // Opt-in only: an emitted instance method can collide with hand-written members.
    public bool GenerateCloneMethod { get; set; }
}
