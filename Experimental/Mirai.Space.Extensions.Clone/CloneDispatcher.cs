using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace Mirai.Space.Extensions.Clone;

internal static class CloneDispatcher
{
    private static readonly HashSet<Type> ImmutableTypes =
    [
        typeof(string),
        typeof(decimal),
        typeof(DateTime),
        typeof(DateTimeOffset),
        typeof(TimeSpan),
        typeof(Guid),
        typeof(Uri),
        typeof(Version),
        typeof(DateOnly),
        typeof(TimeOnly),
        typeof(Int128),
        typeof(UInt128),
        typeof(Half),
        typeof(System.Numerics.BigInteger),
        typeof(System.Numerics.Complex),
        typeof(Rune),
        typeof(Index),
        typeof(Range),
        typeof(Type),
        typeof(MemberInfo),
        typeof(Delegate),
        typeof(Task),
        typeof(Stream),
        typeof(CancellationToken),
        typeof(CancellationTokenSource),
        typeof(Lock),
        typeof(SemaphoreSlim),
    ];

    internal static bool IsImmutable(Type type)
    {
        if (type.IsPrimitive || type.IsEnum)
        {
            return true;
        }

        if (ImmutableTypes.Contains(type))
        {
            return true;
        }

        return typeof(Delegate).IsAssignableFrom(type)
            || typeof(MemberInfo).IsAssignableFrom(type)
            || typeof(Task).IsAssignableFrom(type)
            || typeof(Stream).IsAssignableFrom(type);
    }

    internal static bool IsCollectionLike(Type type)
    {
        if (type.IsArray || typeof(IDictionary).IsAssignableFrom(type) || typeof(IList).IsAssignableFrom(type))
        {
            return true;
        }

        foreach (var interfaceType in type.GetInterfaces())
        {
            if (!interfaceType.IsGenericType)
            {
                continue;
            }

            var definition = interfaceType.GetGenericTypeDefinition();
            if (definition == typeof(IDictionary<,>) || definition == typeof(ICollection<>) || definition == typeof(ISet<>))
            {
                return true;
            }
        }

        return false;
    }

    internal static object? ShallowCloneObject(object? value)
    {
        if (value is null)
        {
            return null;
        }

        var type = value.GetType();
        if (IsImmutable(type))
        {
            return value;
        }

        if (type.IsArray)
        {
            return ((Array)value).Clone();
        }

        if (value is IDictionary dictionary)
        {
            return CloneDictionaryShallow(dictionary, type);
        }

        if (value is IList list)
        {
            return CloneListShallow(list, type);
        }

        if (TryGetCollectionElementType(type, out var elementType))
        {
            return CloneCollectionShallow(value, type, elementType);
        }

        return CloneRuntime.MemberwiseCloneObject(value);
    }

    internal static object? DeepCloneObject(object? value, CloneContext context)
    {
        if (value is null)
        {
            return null;
        }

        var type = value.GetType();
        if (IsImmutable(type))
        {
            return value;
        }

        if (!type.IsValueType && context.TryGetReference(value, out var existing))
        {
            return existing;
        }

        if (type.IsArray)
        {
            return DeepCloneArray((Array)value, context);
        }

        if (value is IDictionary dictionary)
        {
            return DeepCloneDictionary(dictionary, type, context);
        }

        if (value is IList list)
        {
            return DeepCloneList(list, type, context);
        }

        if (TryGetCollectionElementType(type, out var elementType))
        {
            return DeepCloneCollection(value, type, elementType, context);
        }

        return ClonePlanInvoker.Deep(value, context);
    }

    private static object CloneDictionaryShallow(IDictionary dictionary, Type type)
    {
        if (dictionary.IsReadOnly || type.GetConstructor(Type.EmptyTypes) is null)
        {
            return CloneRuntime.MemberwiseCloneObject(dictionary);
        }

        var clone = (IDictionary?)Activator.CreateInstance(type);
        if (clone is null)
        {
            return CloneRuntime.MemberwiseCloneObject(dictionary);
        }

        foreach (DictionaryEntry entry in dictionary)
        {
            clone.Add(entry.Key, entry.Value);
        }

        return clone;
    }

    private static object CloneListShallow(IList list, Type type)
    {
        if (list.IsReadOnly || list.IsFixedSize || type.GetConstructor(Type.EmptyTypes) is null)
        {
            return CloneRuntime.MemberwiseCloneObject(list);
        }

        var clone = (IList?)Activator.CreateInstance(type);
        if (clone is null)
        {
            return CloneRuntime.MemberwiseCloneObject(list);
        }

        foreach (var item in list)
        {
            clone.Add(item);
        }

        return clone;
    }

    private static object CloneCollectionShallow(object value, Type type, Type elementType)
    {
        if (type.GetConstructor(Type.EmptyTypes) is null || IsReadOnlyCollection(value, elementType))
        {
            return CloneRuntime.MemberwiseCloneObject(value);
        }

        var clone = Activator.CreateInstance(type);
        var add = typeof(ICollection<>).MakeGenericType(elementType).GetMethod("Add");
        if (clone is null || add is null)
        {
            return CloneRuntime.MemberwiseCloneObject(value);
        }

        foreach (var item in (IEnumerable)value)
        {
            add.Invoke(clone, [item]);
        }

        return clone;
    }

    private static object DeepCloneArray(Array array, CloneContext context)
    {
        var elementType = array.GetType().GetElementType()!;
        var lengths = new int[array.Rank];
        var lowerBounds = new int[array.Rank];
        for (var dimension = 0; dimension < array.Rank; dimension++)
        {
            lengths[dimension] = array.GetLength(dimension);
            lowerBounds[dimension] = array.GetLowerBound(dimension);
        }

        var clone = Array.CreateInstance(elementType, lengths, lowerBounds);
        context.RegisterReference(array, clone);

        var indices = new int[array.Rank];
        for (var dimension = 0; dimension < array.Rank; dimension++)
        {
            indices[dimension] = array.GetLowerBound(dimension);
        }

        for (var count = 0; count < array.Length; count++)
        {
            clone.SetValue(DeepCloneObject(array.GetValue(indices), context), indices);
            IncrementIndices(array, indices);
        }

        return clone;
    }

    private static object DeepCloneDictionary(IDictionary dictionary, Type type, CloneContext context)
    {
        if (dictionary.IsReadOnly || type.GetConstructor(Type.EmptyTypes) is null)
        {
            return ShareReference(dictionary, context);
        }

        var clone = (IDictionary?)Activator.CreateInstance(type);
        if (clone is null)
        {
            return ShareReference(dictionary, context);
        }

        context.RegisterReference(dictionary, clone);
        foreach (DictionaryEntry entry in dictionary)
        {
            clone.Add(DeepCloneObject(entry.Key, context)!, DeepCloneObject(entry.Value, context));
        }

        return clone;
    }

    private static object DeepCloneList(IList list, Type type, CloneContext context)
    {
        if (list.IsReadOnly || list.IsFixedSize || type.GetConstructor(Type.EmptyTypes) is null)
        {
            return ShareReference(list, context);
        }

        var clone = (IList?)Activator.CreateInstance(type);
        if (clone is null)
        {
            return ShareReference(list, context);
        }

        context.RegisterReference(list, clone);
        foreach (var item in list)
        {
            clone.Add(DeepCloneObject(item, context));
        }

        return clone;
    }

    private static object DeepCloneCollection(object value, Type type, Type elementType, CloneContext context)
    {
        if (type.GetConstructor(Type.EmptyTypes) is null || IsReadOnlyCollection(value, elementType))
        {
            return ShareReference(value, context);
        }

        var clone = Activator.CreateInstance(type);
        var add = typeof(ICollection<>).MakeGenericType(elementType).GetMethod("Add");
        if (clone is null || add is null)
        {
            return ShareReference(value, context);
        }

        context.RegisterReference(value, clone);
        foreach (var item in (IEnumerable)value)
        {
            add.Invoke(clone, [DeepCloneObject(item, context)]);
        }

        return clone;
    }

    private static object ShareReference(object value, CloneContext context)
    {
        var clone = CloneRuntime.MemberwiseCloneObject(value);
        context.RegisterReference(value, clone);
        return clone;
    }

    private static bool TryGetCollectionElementType(Type type, out Type elementType)
    {
        foreach (var interfaceType in type.GetInterfaces())
        {
            if (interfaceType.IsGenericType && interfaceType.GetGenericTypeDefinition() == typeof(ICollection<>))
            {
                elementType = interfaceType.GetGenericArguments()[0];
                return true;
            }
        }

        elementType = null!;
        return false;
    }

    private static bool IsReadOnlyCollection(object value, Type elementType)
    {
        var property = typeof(ICollection<>).MakeGenericType(elementType).GetProperty("IsReadOnly");
        return property is not null && property.GetValue(value) is true;
    }

    private static void IncrementIndices(Array array, int[] indices)
    {
        for (var dimension = array.Rank - 1; dimension >= 0; dimension--)
        {
            indices[dimension]++;
            if (indices[dimension] < array.GetLowerBound(dimension) + array.GetLength(dimension))
            {
                return;
            }

            indices[dimension] = array.GetLowerBound(dimension);
        }
    }

    private static class ClonePlanInvoker
    {
        private static readonly ConcurrentDictionary<Type, Func<object, CloneContext, object>> DeepPlans = new();
        private static readonly MethodInfo DeepCoreMethod =
            typeof(ClonePlanInvoker).GetMethod(nameof(DeepCore), BindingFlags.Static | BindingFlags.NonPublic)!;

        internal static object Deep(object value, CloneContext context) =>
            DeepPlans.GetOrAdd(value.GetType(), CreatePlan)(value, context);

        private static Func<object, CloneContext, object> CreatePlan(Type type) =>
            DeepCoreMethod.MakeGenericMethod(type).CreateDelegate<Func<object, CloneContext, object>>();

        private static object DeepCore<T>(object value, CloneContext context) =>
            CloneCache<T>.Plan.Deep((T)value, context)!;
    }
}
