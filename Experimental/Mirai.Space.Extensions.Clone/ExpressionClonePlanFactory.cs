using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Mirai.Space.Extensions.Clone;

internal static class ExpressionClonePlanFactory<T>
{
    private static readonly Member[] Members = BuildMembers();

    internal static ClonePlan<T> Create() =>
        new(
            static value => value is null ? default! : (T)CloneRuntime.MemberwiseCloneObject(value),
            static (value, context) => value is null ? default! : (T)DeepClone(value, context));

    private static object DeepClone(object value, CloneContext context)
    {
        if (!typeof(T).IsValueType)
        {
            if (context.TryGetReference(value, out var existing))
            {
                return existing;
            }
        }

        var clone = CloneRuntime.MemberwiseCloneObject(value);
        if (!typeof(T).IsValueType)
        {
            context.RegisterReference(value, clone);
        }

        foreach (var member in Members)
        {
            member.Set(clone, CloneDispatcher.DeepCloneObject(member.Get(value), context));
        }

        return clone;
    }

    private static Member[] BuildMembers()
    {
        var type = typeof(T);
        var members = new List<Member>();
        for (var current = type; current is not null && current != typeof(object) && current != typeof(ValueType); current = current.BaseType)
        {
            foreach (var field in current.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (field.IsStatic || field.IsLiteral || field.IsDefined(typeof(CloneIgnoreAttribute), true))
                {
                    continue;
                }

                var property = FindBackingProperty(current, field);
                if (property?.IsDefined(typeof(CloneIgnoreAttribute), true) is true)
                {
                    continue;
                }

                if (field.IsInitOnly)
                {
                    // Expression trees cannot assign initonly fields; reflection still can, and this
                    // covers record positional properties without invoking their init setters.
                    members.Add(new Member(
                        target => field.GetValue(target),
                        (target, value) => field.SetValue(target, value)));
                    continue;
                }

                // Event backing fields and other compiler-generated caches must stay shared.
                if (field.IsDefined(typeof(CompilerGeneratedAttribute), false) && property is null)
                {
                    continue;
                }

                members.Add(CreateFieldMember(field));
            }
        }

        return members.ToArray();
    }

    private static PropertyInfo? FindBackingProperty(Type declaringType, FieldInfo field)
    {
        var name = field.Name;
        if (!name.StartsWith('<') || !name.EndsWith(">k__BackingField", StringComparison.Ordinal))
        {
            return null;
        }

        var end = name.IndexOf('>');
        if (end <= 1)
        {
            return null;
        }

        return declaringType.GetProperty(
            name[1..end],
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
    }

    private static Member CreateFieldMember(FieldInfo field)
    {
        if (typeof(T).IsValueType)
        {
            return new Member(
                target => field.GetValue(target),
                (target, value) => field.SetValue(target, value));
        }

        try
        {
            return new Member(CreateFieldGetter(field), CreateFieldSetter(field));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or MemberAccessException)
        {
            return new Member(
                target => field.GetValue(target),
                (target, value) => field.SetValue(target, value));
        }
    }

    private static Func<object, object?> CreateFieldGetter(FieldInfo field)
    {
        var target = Expression.Parameter(typeof(object), "target");
        var body = Expression.Convert(
            Expression.Field(Expression.Convert(target, field.DeclaringType!), field),
            typeof(object));
        return Expression.Lambda<Func<object, object?>>(body, target).Compile();
    }

    private static Action<object, object?> CreateFieldSetter(FieldInfo field)
    {
        var target = Expression.Parameter(typeof(object), "target");
        var value = Expression.Parameter(typeof(object), "value");
        var body = Expression.Assign(
            Expression.Field(Expression.Convert(target, field.DeclaringType!), field),
            Expression.Convert(value, field.FieldType));
        return Expression.Lambda<Action<object, object?>>(body, target, value).Compile();
    }

    private sealed class Member
    {
        internal Member(Func<object, object?> get, Action<object, object?> set)
        {
            Get = get;
            Set = set;
        }

        internal Func<object, object?> Get { get; }

        internal Action<object, object?> Set { get; }
    }
}
