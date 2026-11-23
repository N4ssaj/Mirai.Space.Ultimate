using PolyType.Abstractions;

namespace Mirai.Space.Extensions.ComponentModel;

// The non-generic GetDefaultConstructor() extension casts to Func<object>, which fails for value types;
// the visitor keeps the strongly typed delegate and boxes the result afterwards.
internal sealed class DefaultConstructorVisitor : TypeShapeVisitor
{
    internal static DefaultConstructorVisitor Instance { get; } = new();

    private DefaultConstructorVisitor()
    {
    }

    public override object? VisitConstructor<TDeclaringType, TArgumentState>(
        IConstructorShape<TDeclaringType, TArgumentState> constructorShape,
        object? state)
    {
        if (constructorShape.Parameters.Count != 0)
        {
            return null;
        }

        var constructor = constructorShape.GetDefaultConstructor();
        return (Func<object?>)(() => constructor());
    }
}
