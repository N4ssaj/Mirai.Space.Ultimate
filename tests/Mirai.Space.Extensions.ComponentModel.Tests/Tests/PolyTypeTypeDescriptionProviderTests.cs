using System.ComponentModel;
using PolyType;
using PolyType.Abstractions;
using Xunit;

namespace Mirai.Space.Extensions.ComponentModel.Tests;

public sealed class PolyTypeTypeDescriptionProviderTests
{
    private static readonly PolyTypeTypeDescriptionProvider Provider = PolyTypeTypeDescriptionProvider.For<PersonModel, ComponentModelWitness>();

    [Fact]
    public void GetTypeDescriptor_ListsShapePropertiesInShapeOrder()
    {
        var objectShape = Assert.IsAssignableFrom<IObjectTypeShape>(Provider.ShapeProvider.GetTypeShape(typeof(PersonModel)));
        var descriptor = Assert.IsAssignableFrom<ICustomTypeDescriptor>(Provider.GetTypeDescriptor(typeof(PersonModel), null));

        var names = descriptor.GetProperties().Cast<PropertyDescriptor>().Select(property => property.Name);

        Assert.Equal(objectShape.Properties.Select(property => property.Name), names);
    }

    [Fact]
    public void GetAttributes_ReturnsTypeLevelDescription()
    {
        var descriptor = Assert.IsAssignableFrom<ICustomTypeDescriptor>(Provider.GetTypeDescriptor(typeof(PersonModel), null));

        var description = Assert.IsType<DescriptionAttribute>(descriptor.GetAttributes()[typeof(DescriptionAttribute)]);

        Assert.Equal("A person described by a PolyType shape.", description.Description);
    }

    [Fact]
    public void GetProperties_ExposesPropertyAttributesFromShape()
    {
        var descriptor = Provider.GetTypeDescriptor(typeof(PersonModel), null)!;

        var name = Assert.IsAssignableFrom<PropertyDescriptor>(descriptor.GetProperties()[nameof(PersonModel.Name)]);
        var description = Assert.IsType<DescriptionAttribute>(name.Attributes[typeof(DescriptionAttribute)]);

        Assert.Equal("The display name.", description.Description);
        Assert.False(name.Attributes.Contains(new BrowsableAttribute(false)));
    }

    [Fact]
    public void GetProperties_FiltersByAttributes()
    {
        var descriptor = Provider.GetTypeDescriptor(typeof(PersonModel), null)!;

        var hidden = descriptor.GetProperties([new BrowsableAttribute(false)]);

        var property = Assert.Single(hidden.Cast<PropertyDescriptor>());
        Assert.Equal(nameof(PersonModel.Secret), property.Name);
    }

    [Fact]
    public void GetProperties_FilterWithoutMatches_ReturnsEmpty()
    {
        var descriptor = Provider.GetTypeDescriptor(typeof(PersonModel), null)!;

        var filtered = descriptor.GetProperties([new CategoryAttribute("Missing")]);

        Assert.Empty(filtered.Cast<PropertyDescriptor>());
    }

    [Fact]
    public void GetTypeDescriptor_NonObjectShapeFallsBackToParent()
    {
        var descriptor = Provider.GetTypeDescriptor(typeof(int), 42);

        Assert.NotNull(descriptor);
        Assert.Empty(descriptor.GetProperties().Cast<PropertyDescriptor>());
    }

    [Fact]
    public void GetClassName_ReturnsFullName()
    {
        var descriptor = Provider.GetTypeDescriptor(typeof(PersonModel), null)!;

        Assert.Equal(typeof(PersonModel).FullName, descriptor.GetClassName());
    }

    [Fact]
    public void CreateInstance_UsesShapeDefaultConstructor()
    {
        var instance = Provider.CreateInstance(null, typeof(PersonModel), null, null);
        var boxedPoint = Provider.CreateInstance(null, typeof(PointModel), [], []);

        Assert.IsType<PersonModel>(instance);
        Assert.IsType<PointModel>(boxedPoint);
    }

    [Fact]
    public void GetDefaultProperty_UsesDefaultPropertyAttribute()
    {
        var person = Provider.GetTypeDescriptor(typeof(PersonModel), null)!;
        var point = Provider.GetTypeDescriptor(typeof(PointModel), null)!;

        Assert.Equal(nameof(PersonModel.Name), person.GetDefaultProperty()?.Name);
        Assert.Null(point.GetDefaultProperty());
    }

    [Fact]
    public void GetDefaultEvent_UsesDefaultEventAttribute()
    {
        var descriptor = Provider.GetTypeDescriptor(typeof(EventfulModel), null)!;

        Assert.Equal(nameof(EventfulModel.Changed), descriptor.GetDefaultEvent()?.Name);
    }

    [Fact]
    public void GetTypeDescriptor_CachesDescriptorPerType()
    {
        var first = Provider.GetTypeDescriptor(typeof(PersonModel), null);
        var second = Provider.GetTypeDescriptor(typeof(PersonModel), null);

        Assert.Same(first, second);
    }

    [Fact]
    public void GetTypeDescriptor_ConcurrentAccess_ReturnsSameDescriptor()
    {
        var descriptors = new ICustomTypeDescriptor?[64];

        Parallel.For(0, descriptors.Length, index =>
            descriptors[index] = Provider.GetTypeDescriptor(typeof(EventfulModel), null));

        var first = descriptors[0];
        Assert.NotNull(first);
        Assert.All(descriptors, descriptor => Assert.Same(first, descriptor));
    }

    [Fact]
    public void GetAttributes_ReturnsCachedCollection()
    {
        var descriptor = Provider.GetTypeDescriptor(typeof(PersonModel), null)!;

        Assert.Same(descriptor.GetAttributes(), descriptor.GetAttributes());
    }

    [Fact]
    public void CreateTypeDescriptionProvider_ReusesShapeProvider()
    {
        var provider = Provider.ShapeProvider.CreateTypeDescriptionProvider();

        Assert.Same(Provider.ShapeProvider, provider.ShapeProvider);
        Assert.Throws<ArgumentNullException>(() => ((ITypeShapeProvider)null!).CreateTypeDescriptionProvider());
    }

    [Fact]
    public void For_DirectlyAnnotatedType_UsesItsGeneratedShape()
    {
        var provider = PolyTypeTypeDescriptionProvider.For<PointModel>();
        var descriptor = provider.GetTypeDescriptor(typeof(PointModel), null)!;

        var names = descriptor.GetProperties().Cast<PropertyDescriptor>().Select(property => property.Name);

        Assert.Equal(new[] { nameof(PointModel.X), nameof(PointModel.Y) }, names);
    }

    [Fact]
    public void GetProperties_EmptyFilter_ReturnsCachedCollection()
    {
        var descriptor = Provider.GetTypeDescriptor(typeof(PersonModel), null)!;

        Assert.Same(descriptor.GetProperties(), descriptor.GetProperties());
        Assert.Same(descriptor.GetProperties(), descriptor.GetProperties(null));
    }

    [Fact]
    public void GetEvents_EmptyFilter_ReturnsCachedCollection()
    {
        var descriptor = Provider.GetTypeDescriptor(typeof(EventfulModel), null)!;

        Assert.Same(descriptor.GetEvents(), descriptor.GetEvents());
        Assert.Same(descriptor.GetEvents(), descriptor.GetEvents(null));
    }

    [Fact]
    public void TypeDescriptor_ExposesClassMetadata()
    {
        TypeDescriptor.AddProvider(Provider, typeof(PersonModel));
        try
        {
            Assert.Equal(typeof(PersonModel).FullName, TypeDescriptor.GetClassName(typeof(PersonModel)));
            Assert.Equal(nameof(PersonModel.Name), TypeDescriptor.GetDefaultProperty(typeof(PersonModel))?.Name);
            Assert.IsType<PersonModel>(TypeDescriptor.CreateInstance(null, typeof(PersonModel), null, null));
        }
        finally
        {
            TypeDescriptor.RemoveProvider(Provider, typeof(PersonModel));
        }
    }

    [Fact]
    public void TypeDescriptor_UsesRegisteredProvider()
    {
        TypeDescriptor.AddProvider(Provider, typeof(PersonModel));
        try
        {
            var shape = Assert.IsAssignableFrom<IObjectTypeShape>(Provider.ShapeProvider.GetTypeShape(typeof(PersonModel)));

            var names = TypeDescriptor.GetProperties(typeof(PersonModel))
                .Cast<PropertyDescriptor>()
                .Select(property => property.Name);

            Assert.Equal(shape.Properties.Select(property => property.Name), names);
        }
        finally
        {
            TypeDescriptor.RemoveProvider(Provider, typeof(PersonModel));
        }
    }

    [Fact]
    public void TypeDescriptor_GetPropertiesFromInstance_UsesShape()
    {
        TypeDescriptor.AddProvider(Provider, typeof(PersonModel));
        try
        {
            var shape = Assert.IsAssignableFrom<IObjectTypeShape>(Provider.ShapeProvider.GetTypeShape(typeof(PersonModel)));
            var names = TypeDescriptor.GetProperties(new PersonModel())
                .Cast<PropertyDescriptor>()
                .Select(property => property.Name);

            Assert.Equal(shape.Properties.Select(property => property.Name), names);
        }
        finally
        {
            TypeDescriptor.RemoveProvider(Provider, typeof(PersonModel));
        }
    }

    [Fact]
    public void WriteOnlyProperty_DescriptorBuildsAndThrowsOnRead()
    {
        var descriptor = Provider.GetTypeDescriptor(typeof(WriteOnlyModel), null)!;
        var secret = Assert.IsAssignableFrom<PropertyDescriptor>(descriptor.GetProperties()[nameof(WriteOnlyModel.Secret)]);
        var model = new WriteOnlyModel();

        Assert.False(secret.IsReadOnly);
        Assert.Throws<InvalidOperationException>(() => secret.GetValue(model));

        secret.SetValue(model, 42);

        Assert.Equal(42, model.LastValue);
    }

    [Fact]
    public void GetDefaultProperty_AttributeWithUnknownName_DoesNotFallBackToParent()
    {
        var provider = new PolyTypeTypeDescriptionProvider(
            Provider.ShapeProvider,
            new ParentWithDefaultPropertyProvider());
        var descriptor = provider.GetTypeDescriptor(typeof(BrokenDefaultModel), null)!;

        Assert.Null(descriptor.GetDefaultProperty());
    }

    [Fact]
    public void TypeDescriptor_AddProvider_PreservesReflectionFallback()
    {
        TypeDescriptor.AddProvider(Provider, typeof(Uri));
        try
        {
            var names = TypeDescriptor.GetProperties(typeof(Uri))
                .Cast<PropertyDescriptor>()
                .Select(property => property.Name);

            Assert.Contains(nameof(Uri.AbsoluteUri), names);
        }
        finally
        {
            TypeDescriptor.RemoveProvider(Provider, typeof(Uri));
        }
    }

    private sealed class ParentWithDefaultPropertyProvider : TypeDescriptionProvider
    {
        public override ICustomTypeDescriptor GetTypeDescriptor(Type objectType, object? instance) => new ParentDescriptor();

        private sealed class ParentDescriptor : CustomTypeDescriptor
        {
            public override PropertyDescriptor? GetDefaultProperty() => new ParentPropertyDescriptor();
        }

        private sealed class ParentPropertyDescriptor : PropertyDescriptor
        {
            internal ParentPropertyDescriptor() : base("ParentDefault", null)
            {
            }

            public override Type ComponentType => typeof(object);

            public override bool IsReadOnly => true;

            public override Type PropertyType => typeof(object);

            public override bool CanResetValue(object component) => false;

            public override object? GetValue(object? component) => null;

            public override void ResetValue(object component)
            {
            }

            public override void SetValue(object? component, object? value)
            {
            }

            public override bool ShouldSerializeValue(object component) => false;
        }
    }
}