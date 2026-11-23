using PolyType;
using PolyType.Abstractions;

namespace Mirai.Space.Extensions.Clone;

internal static class PolyTypeClonePlanFactory<T>
{
    internal static bool TryCreate(out ClonePlan<T> plan)
    {
        plan = null!;
        if (!typeof(IShapeable<T>).IsAssignableFrom(typeof(T)))
        {
            return false;
        }

        var shape = TypeShapeResolver.ResolveDynamic<T>();
        if (shape is not IObjectTypeShape<T>)
        {
            return false;
        }

        if (shape.Accept(new PlanVisitor(), null) is not ClonePlan<T> created)
        {
            return false;
        }

        plan = created;
        return true;
    }

    private sealed class PlanVisitor : TypeShapeVisitor
    {
        public override object? VisitObject<TShape>(IObjectTypeShape<TShape> objectShape, object? state)
        {
            var members = new List<MemberAccessor<TShape>>();
            foreach (var property in objectShape.Properties)
            {
                if (property.AttributeProvider.IsDefined<CloneIgnoreAttribute>(true))
                {
                    continue;
                }

                if (!property.HasGetter)
                {
                    continue;
                }

                if (!property.HasSetter)
                {
                    // A get-only/init-only property may hold state the shape cannot assign.
                    // Reject the plan so the field-based Expression fallback can clone it.
                    return null;
                }

                if (property.Accept(this, null) is MemberAccessor<TShape> accessor)
                {
                    members.Add(accessor);
                }
            }

            var accessors = members.ToArray();
            if (accessors.Length == 0)
            {
                return null;
            }

            if (typeof(TShape).IsValueType)
            {
                return new ClonePlan<TShape>(
                    static value => value,
                    (value, context) =>
                    {
                        var clone = value;
                        foreach (var accessor in accessors)
                        {
                            accessor.Set(ref clone, context.Clone(accessor.Get(value)));
                        }

                        return clone;
                    });
            }

            return new ClonePlan<TShape>(
                static value => (TShape)CloneRuntime.MemberwiseCloneObject(value!),
                (value, context) =>
                {
                    if (context.TryGetReference(value!, out var existing))
                    {
                        return (TShape)existing;
                    }

                    var clone = (TShape)CloneRuntime.MemberwiseCloneObject(value!);
                    context.RegisterReference(value!, clone);
                    foreach (var accessor in accessors)
                    {
                        accessor.Set(ref clone, context.Clone(accessor.Get(value)));
                    }

                    return clone;
                });
        }

        public override object? VisitProperty<TDeclaring, TProperty>(
            IPropertyShape<TDeclaring, TProperty> propertyShape,
            object? state)
        {
            var getter = propertyShape.GetGetter();
            var setter = propertyShape.GetSetter();
            return new MemberAccessor<TDeclaring>(
                value =>
                {
                    var target = value;
                    return getter(ref target);
                },
                (ref TDeclaring target, object? value) => setter(ref target, (TProperty)value!));
        }
    }

    private sealed class MemberAccessor<TShape>
    {
        internal MemberAccessor(Func<TShape, object?> get, Setter set)
        {
            Get = get;
            Set = set;
        }

        internal Func<TShape, object?> Get { get; }

        internal Setter Set { get; }

        internal delegate void Setter(ref TShape target, object? value);
    }
}
