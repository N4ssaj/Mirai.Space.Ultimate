namespace Mirai.Space.Extensions.Clone;

internal static class CloneCache<T>
{
    internal static readonly CloneStrategy Strategy;
    internal static readonly ClonePlan<T> Plan;

    static CloneCache()
    {
        Plan = Resolve(out Strategy);
    }

    private static ClonePlan<T> Resolve(out CloneStrategy strategy)
    {
        var type = typeof(T);
        if (CloneDispatcher.IsImmutable(type))
        {
            strategy = CloneStrategy.RuntimeDispatch;
            return new ClonePlan<T>(static value => value, static (value, _) => value);
        }

        if (CloneDispatcher.IsCollectionLike(type))
        {
            strategy = CloneStrategy.RuntimeDispatch;
            return new ClonePlan<T>(
                static value => (T)CloneDispatcher.ShallowCloneObject(value)!,
                static (value, context) => (T)CloneDispatcher.DeepCloneObject(value, context)!);
        }

        if (type.IsInterface || type.IsAbstract || type == typeof(object) || Nullable.GetUnderlyingType(type) is not null)
        {
            strategy = CloneStrategy.RuntimeDispatch;
            return new ClonePlan<T>(
                static value => (T)CloneDispatcher.ShallowCloneObject(value)!,
                static (value, context) => (T)CloneDispatcher.DeepCloneObject(value, context)!);
        }

        if (CloneRegistry.TryGet<T>(out var generated))
        {
            strategy = CloneStrategy.SourceGenerated;
            return generated;
        }

        if (PolyTypeClonePlanFactory<T>.TryCreate(out var polyTypePlan))
        {
            strategy = CloneStrategy.PolyType;
            return polyTypePlan;
        }

        strategy = CloneStrategy.Expression;
        return ExpressionClonePlanFactory<T>.Create();
    }
}
