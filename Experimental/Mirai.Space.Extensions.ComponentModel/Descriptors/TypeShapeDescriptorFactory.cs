using System.ComponentModel;
using PolyType;
using PolyType.Abstractions;

namespace Mirai.Space.Extensions.ComponentModel;

internal sealed class TypeShapeDescriptorFactory
{
    private readonly ITypeShapeProvider _shapeProvider;
    private readonly DescriptorShapeVisitor _visitor;

    internal TypeShapeDescriptorFactory(ITypeShapeProvider shapeProvider)
    {
        _shapeProvider = shapeProvider;
        _visitor = new DescriptorShapeVisitor(this);
    }

    internal PolyTypeTypeDescriptor? Create(Type objectType, ICustomTypeDescriptor? parentDescriptor)
    {
        return _shapeProvider.GetTypeShape(objectType) is IObjectTypeShape objectShape
            ? Create(objectShape, parentDescriptor)
            : null;
    }

    internal PolyTypeTypeDescriptor Create(IObjectTypeShape objectShape, ICustomTypeDescriptor? parentDescriptor)
    {
        // Getter/setter delegates only exist on the generic shapes, so every entry goes through Accept.
        var properties = objectShape.Properties
            .Select(property => (PropertyDescriptor)property.Accept(_visitor, null)!)
            .ToArray();

        var attributes = objectShape.AttributeProvider.GetAttributes();
        if (attributes.Length == 0 && parentDescriptor is not null)
        {
            attributes = [.. parentDescriptor.GetAttributes().Cast<Attribute>()];
        }

        var events = objectShape.Events.Count > 0
            ? objectShape.Events.Select(descriptor => (EventDescriptor)descriptor.Accept(_visitor, null)!).ToArray()
            : ParentEvents(parentDescriptor);

        return new PolyTypeTypeDescriptor(objectShape.Type, attributes, properties, events);
    }

    private static EventDescriptor[] ParentEvents(ICustomTypeDescriptor? parentDescriptor)
        => parentDescriptor is null ? [] : [.. parentDescriptor.GetEvents().Cast<EventDescriptor>()];
}
