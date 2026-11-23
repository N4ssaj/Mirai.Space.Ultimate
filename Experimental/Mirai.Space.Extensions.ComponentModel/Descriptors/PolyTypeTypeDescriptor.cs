using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace Mirai.Space.Extensions.ComponentModel;

internal sealed class PolyTypeTypeDescriptor : CustomTypeDescriptor
{
    private readonly Type _type;
    private readonly AttributeCollection _attributes;
    private readonly PropertyDescriptorCollection _properties;
    private readonly EventDescriptorCollection _events;
    private readonly bool _hasDefaultProperty;
    private readonly PropertyDescriptor? _defaultProperty;
    private readonly bool _hasDefaultEvent;
    private readonly EventDescriptor? _defaultEvent;

    internal PolyTypeTypeDescriptor(Type type, Attribute[] attributes, PropertyDescriptor[] properties, EventDescriptor[] events)
    {
        _type = type;
        _attributes = new AttributeCollection(attributes);
        _properties = new PropertyDescriptorCollection(properties, readOnly: true);
        _events = new EventDescriptorCollection(events, readOnly: true);

        var defaultPropertyName = attributes.OfType<DefaultPropertyAttribute>().FirstOrDefault()?.Name;
        _hasDefaultProperty = defaultPropertyName is not null;
        _defaultProperty = defaultPropertyName is null ? null : _properties[defaultPropertyName];

        var defaultEventName = attributes.OfType<DefaultEventAttribute>().FirstOrDefault()?.Name;
        _hasDefaultEvent = defaultEventName is not null;
        _defaultEvent = defaultEventName is null ? null : _events[defaultEventName];
    }

    public override string GetClassName() => _type.FullName ?? _type.Name;

    public override AttributeCollection GetAttributes() => _attributes;

    [RequiresUnreferencedCode("ICustomTypeDescriptor marks this member RequiresUnreferencedCode; this override reads only PolyType shapes.")]
    public override PropertyDescriptor? GetDefaultProperty() =>
        _hasDefaultProperty ? _defaultProperty : base.GetDefaultProperty();

    [RequiresUnreferencedCode("ICustomTypeDescriptor marks this member RequiresUnreferencedCode; this override reads only PolyType shapes.")]
    public override EventDescriptor? GetDefaultEvent() =>
        _hasDefaultEvent ? _defaultEvent : base.GetDefaultEvent();

    // ICustomTypeDescriptor marks these members RequiresUnreferencedCode, so the trimming analyzer
    // (IL2046) requires the override to repeat the annotation regardless of what it does. Nothing
    // below reflects; the shape already carries every descriptor.
    [RequiresUnreferencedCode("ICustomTypeDescriptor marks this member RequiresUnreferencedCode; this override reads only PolyType shapes.")]
    public override PropertyDescriptorCollection GetProperties() => _properties;

    [RequiresUnreferencedCode("ICustomTypeDescriptor marks this member RequiresUnreferencedCode; this override reads only PolyType shapes.")]
    public override PropertyDescriptorCollection GetProperties(Attribute[]? attributes) => FilterProperties(attributes);

    public override EventDescriptorCollection GetEvents() => _events;

    [RequiresUnreferencedCode("ICustomTypeDescriptor marks this member RequiresUnreferencedCode; this override reads only PolyType shapes.")]
    public override EventDescriptorCollection GetEvents(Attribute[]? attributes) => FilterEvents(attributes);

    private PropertyDescriptorCollection FilterProperties(Attribute[]? attributes)
    {
        if (attributes is null || attributes.Length == 0)
        {
            return _properties;
        }

        return new PropertyDescriptorCollection(
            [.. _properties.Cast<PropertyDescriptor>().Where(property => property.Attributes.Matches(attributes))],
            readOnly: true);
    }

    private EventDescriptorCollection FilterEvents(Attribute[]? attributes)
    {
        if (attributes is null || attributes.Length == 0)
        {
            return _events;
        }

        return new EventDescriptorCollection(
            [.. _events.Cast<EventDescriptor>().Where(descriptor => descriptor.Attributes.Matches(attributes))],
            readOnly: true);
    }
}
