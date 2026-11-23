using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using PolyType.Abstractions;

namespace Mirai.Space.Extensions.ComponentModel;

internal sealed class PolyTypePropertyDescriptor<TDeclaringType, TPropertyType> : PropertyDescriptor
{
    private readonly Getter<TDeclaringType, TPropertyType>? _getter;
    private readonly Setter<TDeclaringType, TPropertyType>? _setter;
    private readonly TypeShapeDescriptorFactory _descriptorFactory;
    private readonly object? _defaultValue;
    private readonly bool _hasDefaultValue;
    private readonly bool _hasChangedEvent;
    private PolyTypeTypeDescriptor? _childDescriptor;

    public override Type ComponentType => typeof(TDeclaringType);

    public override bool IsReadOnly =>
        _setter is null || Attributes[typeof(ReadOnlyAttribute)] is ReadOnlyAttribute { IsReadOnly: true };

    public override Type PropertyType => typeof(TPropertyType);

    public override bool SupportsChangeEvents =>
        typeof(INotifyPropertyChanged).IsAssignableFrom(typeof(TDeclaringType)) || _hasChangedEvent;

    internal PolyTypePropertyDescriptor(
        IPropertyShape<TDeclaringType, TPropertyType> propertyShape,
        TypeShapeDescriptorFactory descriptorFactory)
        : base(propertyShape.Name, propertyShape.AttributeProvider.GetAttributes())
    {
        _getter = propertyShape.HasGetter ? propertyShape.GetGetter() : null;
        _setter = propertyShape.HasSetter ? propertyShape.GetSetter() : null;
        _descriptorFactory = descriptorFactory;
        _hasChangedEvent = propertyShape.DeclaringType.Events.Any(
            @event => @event.Name == propertyShape.Name + "Changed");

        if (Attributes[typeof(DefaultValueAttribute)] is DefaultValueAttribute defaultValue)
        {
            _defaultValue = defaultValue.Value;
            _hasDefaultValue = true;
        }
    }

    public override bool CanResetValue(object component)
    {
        if (component is null || _getter is null || IsReadOnly)
        {
            return false;
        }

        return _hasDefaultValue && !Equals(GetValue(component), _defaultValue);
    }

    [RequiresUnreferencedCode("The base PropertyDescriptor member requires this annotation; the shape path reads no reflection.")]
    public override PropertyDescriptorCollection GetChildProperties(object? instance, Attribute[]? filter)
    {
        if (instance is null)
        {
            var childDescriptor = _childDescriptor;
            if (childDescriptor is null)
            {
                childDescriptor = _descriptorFactory.Create(PropertyType, parentDescriptor: null);
                if (childDescriptor is null)
                {
                    return base.GetChildProperties(instance, filter);
                }

                _childDescriptor = childDescriptor;
            }

            return childDescriptor.GetProperties(filter);
        }

        return base.GetChildProperties(instance, filter);
    }

    public override object? GetValue(object? component)
    {
        if (component is null)
        {
            ThrowHelper.ThrowComponentIsNull();
            return null;
        }

        if (component is TDeclaringType declaringType)
        {
            var getter = _getter;
            if (getter is null)
            {
                ThrowHelper.ThrowPropertyIsWriteOnly(Name);
                return null;
            }

            return getter(ref declaringType);
        }

        ThrowHelper.ThrowComponentTypeMismatch(component.GetType(), typeof(TDeclaringType));
        return null;
    }

    public override void ResetValue(object component)
    {
        if (component is null)
        {
            ThrowHelper.ThrowComponentIsNull();
            return;
        }

        if (!_hasDefaultValue || IsReadOnly)
        {
            return;
        }

        SetValue(component, _defaultValue);
    }

    public override void SetValue(object? component, object? value)
    {
        if (component is null)
        {
            ThrowHelper.ThrowComponentIsNull();
            return;
        }

        var setter = _setter;
        if (IsReadOnly || setter is null)
        {
            ThrowHelper.ThrowPropertyIsReadOnly(Name);
            return;
        }

        var propertyValue = ConvertValue(value);
        if (component is not TDeclaringType declaringType)
        {
            ThrowHelper.ThrowComponentTypeMismatch(component.GetType(), typeof(TDeclaringType));
            return;
        }

        if (typeof(TDeclaringType).IsValueType)
        {
            ThrowHelper.ThrowBoxedValueMutationNotSupported(typeof(TDeclaringType), Name);
            return;
        }

        setter(ref declaringType, propertyValue);
        OnValueChanged(component, EventArgs.Empty);
    }

    public override bool ShouldSerializeValue(object component)
    {
        if (component is null || _getter is null)
        {
            return false;
        }

        if (IsReadOnly)
        {
            return Attributes[typeof(DesignerSerializationVisibilityAttribute)] is
                DesignerSerializationVisibilityAttribute { Visibility: DesignerSerializationVisibility.Content };
        }

        return !_hasDefaultValue || !Equals(_defaultValue, GetValue(component));
    }

    private TPropertyType ConvertValue(object? value)
    {
        if (value is TPropertyType propertyValue)
        {
            return propertyValue;
        }

        if (value is null)
        {
            if (typeof(TPropertyType).IsValueType && Nullable.GetUnderlyingType(typeof(TPropertyType)) is null)
            {
                ThrowHelper.ThrowNullValueForNonNullableProperty(Name);
            }

            return default!;
        }

        ThrowHelper.ThrowValueTypeMismatch(value.GetType(), typeof(TPropertyType));
        return default!;
    }
}
