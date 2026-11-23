using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using PolyType;
using PolyType.Abstractions;
using PolyType.Utilities;

namespace Mirai.Space.Extensions.ComponentModel;

public sealed class PolyTypeTypeDescriptionProvider : TypeDescriptionProvider
{
    // TypeDescriptionProvider annotates its parameter with the expanded all-members mask; the
    // analyzer compares the raw enum value, so DynamicallyAccessedMemberTypes.All (-1) does not match.
    private const DynamicallyAccessedMemberTypes AllMembers = (DynamicallyAccessedMemberTypes)4194303;

    private readonly ITypeShapeProvider _shapeProvider;
    private readonly TypeShapeDescriptorFactory _descriptorFactory;
    private readonly TypeCache _descriptors;
    private readonly TypeCache _parents;

    public ITypeShapeProvider ShapeProvider => _shapeProvider;

    // TypeDescriptor does not backfill a provider added via AddProvider, so a parentless provider
    // would expose an empty descriptor for types without a PolyType shape and hide their metadata.
    public PolyTypeTypeDescriptionProvider(ITypeShapeProvider shapeProvider)
        : this(shapeProvider, TypeDescriptor.GetProvider(typeof(object)))
    {
    }

    // The base constructor accepts a null parent but annotates the parameter as non-nullable.
    public PolyTypeTypeDescriptionProvider(ITypeShapeProvider shapeProvider, TypeDescriptionProvider? parent)
        : base(parent!)
    {
        ArgumentNullException.ThrowIfNull(shapeProvider);
        _shapeProvider = shapeProvider;
        _descriptorFactory = new TypeShapeDescriptorFactory(shapeProvider);
        _parents = new TypeCache();
        _descriptors = new TypeCache(shapeProvider)
        {
            // PolyType caches through its generation context so recursive type graphs cannot
            // deadlock; the builder maps the root object shape to a descriptor.
            ValueBuilderFactory = _ => new DescriptorBuilder(this),
        };
    }

    public override ICustomTypeDescriptor? GetTypeDescriptor(
        [DynamicallyAccessedMembers(AllMembers)] Type objectType,
        object? instance)
    {
        ArgumentNullException.ThrowIfNull(objectType);

        if (_descriptors.TryGetValue(objectType, out var cached))
        {
            return (PolyTypeTypeDescriptor?)cached;
        }

        if (_shapeProvider.GetTypeShape(objectType) is not IObjectTypeShape objectShape)
        {
            // No shape means no PolyType metadata; the previous provider keeps owning the type.
            return base.GetTypeDescriptor(objectType, instance);
        }

        // TypeCache.GetOrAdd builds from the shape alone, but the parent descriptor must be resolved
        // while the annotated objectType parameter is still in scope, so stage it per type first.
        _parents[objectType] = base.GetTypeDescriptor(objectType, null);

        return _descriptors.GetOrAdd(objectShape) as PolyTypeTypeDescriptor
            ?? base.GetTypeDescriptor(objectType, instance);
    }

    public override object? CreateInstance(
        IServiceProvider? provider,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type objectType,
        Type[]? argTypes,
        object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(objectType);

        // PolyType can build the parameterless case without Activator; anything with arguments still
        // has to go through the base implementation.
        if (argTypes is null or [] && args is null or [])
        {
            if (_shapeProvider.GetTypeShape(objectType) is IObjectTypeShape { Constructor: { IsPublic: true, Parameters.Count: 0 } constructorShape }
                && constructorShape.Accept(DefaultConstructorVisitor.Instance, null) is Func<object?> constructor)
            {
                return constructor();
            }
        }

        return base.CreateInstance(provider, objectType, argTypes, args);
    }

    public static PolyTypeTypeDescriptionProvider For<T>()
        where T : IShapeable<T> => new(T.GetTypeShape().Provider);

    public static PolyTypeTypeDescriptionProvider For<T, TWitness>()
        where TWitness : IShapeable<T> => new(TWitness.GetTypeShape().Provider);

    private PolyTypeTypeDescriptor CreateDescriptor(IObjectTypeShape objectShape, ICustomTypeDescriptor? parentDescriptor) =>
        _descriptorFactory.Create(objectShape, parentDescriptor);

    private ICustomTypeDescriptor? GetParentDescriptor(Type type) =>
        _parents.TryGetValue(type, out var parent) ? (ICustomTypeDescriptor?)parent : null;

    private sealed class DescriptorBuilder(PolyTypeTypeDescriptionProvider provider) : ITypeShapeFunc
    {
        public object? Invoke<T>(ITypeShape<T> typeShape, object? state = null) =>
            typeShape is IObjectTypeShape objectShape
                ? provider.CreateDescriptor(objectShape, provider.GetParentDescriptor(objectShape.Type))
                : null;
    }
}