using System.ComponentModel;
using PolyType.Abstractions;

namespace Mirai.Space.Extensions.ComponentModel;

internal sealed class PolyTypeEventDescriptor<TDeclaringType, TEventHandler> : EventDescriptor
{
    private readonly Type _eventType;
    private readonly Setter<TDeclaringType?, TEventHandler> _addHandler;
    private readonly Setter<TDeclaringType?, TEventHandler> _removeHandler;

    public override Type ComponentType => typeof(TDeclaringType);

    public override Type EventType => _eventType;

    public override bool IsMulticast => true;

    internal PolyTypeEventDescriptor(IEventShape<TDeclaringType, TEventHandler> eventShape)
        : base(eventShape.Name, eventShape.AttributeProvider.GetAttributes())
    {
        _eventType = eventShape.HandlerType.Type;
        _addHandler = eventShape.GetAddHandler();
        _removeHandler = eventShape.GetRemoveHandler();
    }

    public override void AddEventHandler(object component, Delegate value) => Invoke(_addHandler, component, value);

    public override void RemoveEventHandler(object component, Delegate value) => Invoke(_removeHandler, component, value);

    private static void Invoke(Setter<TDeclaringType?, TEventHandler> handler, object component, Delegate value)
    {
        if (component is null)
        {
            ThrowHelper.ThrowComponentIsNull();
            return;
        }

        if (value is null)
        {
            ThrowHelper.ThrowDelegateIsNull();
            return;
        }

        if (value is not TEventHandler handlerValue)
        {
            ThrowHelper.ThrowDelegateTypeMismatch(value.GetType(), typeof(TEventHandler));
            return;
        }

        if (component is not TDeclaringType)
        {
            ThrowHelper.ThrowComponentTypeMismatch(component.GetType(), typeof(TDeclaringType));
            return;
        }

        if (typeof(TDeclaringType).IsValueType)
        {
            ThrowHelper.ThrowBoxedValueMutationNotSupported(typeof(TDeclaringType), "event");
            return;
        }

        // The shape declares the declaring type as nullable; the component is known to be non-null here.
        var declaringType = (TDeclaringType?)component;
        handler(ref declaringType, handlerValue);
    }
}
