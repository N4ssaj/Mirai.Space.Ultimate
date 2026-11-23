using System.Diagnostics.CodeAnalysis;

namespace Mirai.Space.Extensions.ComponentModel;

internal static class ThrowHelper
{
    [DoesNotReturn]
    internal static void ThrowComponentIsNull() => throw new ArgumentNullException("component", "A component instance is required to access a property or event.");

    [DoesNotReturn]
    internal static void ThrowComponentTypeMismatch(Type actualType, Type expectedType) =>
        throw new ArgumentException($"The component of type '{actualType}' is not assignable to the shape's declaring type '{expectedType}'.", "component");

    [DoesNotReturn]
    internal static void ThrowPropertyIsReadOnly(string propertyName) =>
        throw new InvalidOperationException($"Property '{propertyName}' is read-only because the type shape exposes no setter or the property is marked with ReadOnlyAttribute.");

    [DoesNotReturn]
    internal static void ThrowPropertyIsWriteOnly(string propertyName) =>
        throw new InvalidOperationException($"Property '{propertyName}' is write-only because the type shape exposes no getter.");

    [DoesNotReturn]
    internal static void ThrowValueTypeMismatch(Type actualType, Type expectedType) =>
        throw new ArgumentException($"A value of type '{actualType}' cannot be assigned to a property of type '{expectedType}'.", "value");

    [DoesNotReturn]
    internal static void ThrowNullValueForNonNullableProperty(string propertyName) =>
        throw new ArgumentNullException("value", $"Property '{propertyName}' does not accept null.");

    [DoesNotReturn]
    internal static void ThrowDelegateIsNull() => throw new ArgumentNullException("value", "An event handler delegate is required.");

    [DoesNotReturn]
    internal static void ThrowDelegateTypeMismatch(Type actualType, Type expectedType) =>
        throw new ArgumentException($"A delegate of type '{actualType}' is not assignable to the event handler type '{expectedType}'.", "value");

    // Ref-free unboxing of an arbitrary value type needs Unsafe.Unbox<T>, whose struct constraint
    // cannot be satisfied from an unconstrained generic component type.
    [DoesNotReturn]
    internal static void ThrowBoxedValueMutationNotSupported(Type componentType, string memberName) =>
        throw new NotSupportedException($"Member '{memberName}' belongs to value type '{componentType}'; mutating a boxed value type is not supported.");
}
