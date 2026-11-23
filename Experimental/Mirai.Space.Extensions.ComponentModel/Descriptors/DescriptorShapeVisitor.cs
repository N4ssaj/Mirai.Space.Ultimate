using PolyType.Abstractions;

namespace Mirai.Space.Extensions.ComponentModel;

internal sealed class DescriptorShapeVisitor : TypeShapeVisitor
{
    private readonly TypeShapeDescriptorFactory _descriptorFactory;

    internal DescriptorShapeVisitor(TypeShapeDescriptorFactory descriptorFactory)
    {
        _descriptorFactory = descriptorFactory;
    }

    public override object? VisitProperty<TDeclaringType, TPropertyType>(
        IPropertyShape<TDeclaringType, TPropertyType> propertyShape,
        object? state)
    {
        return new PolyTypePropertyDescriptor<TDeclaringType, TPropertyType>(propertyShape, _descriptorFactory);
    }

    public override object? VisitEvent<TDeclaringType, TEventHandler>(
        IEventShape<TDeclaringType, TEventHandler> eventShape,
        object? state)
    {
        return new PolyTypeEventDescriptor<TDeclaringType, TEventHandler>(eventShape);
    }
}
