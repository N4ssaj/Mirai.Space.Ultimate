using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace Mirai.Space.Extensions.Clone;

[EditorBrowsable(EditorBrowsableState.Never)]
public static class CloneRegistry
{
    private static readonly ConcurrentDictionary<Type, object> Plans = new();

    public static void Register<T>(ClonePlan<T> plan) => Plans[typeof(T)] = plan;

    internal static bool TryGet<T>([NotNullWhen(true)] out ClonePlan<T>? plan)
    {
        if (Plans.TryGetValue(typeof(T), out var value))
        {
            plan = (ClonePlan<T>)value;
            return true;
        }

        plan = null;
        return false;
    }
}
