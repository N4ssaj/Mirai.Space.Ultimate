using System.ComponentModel;
using Xunit;

namespace Mirai.Space.Extensions.ComponentModel.Tests;

public sealed class PolyTypePropertyDescriptorTests
{
    private static readonly PolyTypeTypeDescriptionProvider Provider = PolyTypeTypeDescriptionProvider.For<PersonModel, ComponentModelWitness>();

    [Fact]
    public void GetValue_ReadsThroughShapeGetter()
    {
        var person = new PersonModel { Name = "Ada", Age = 36 };

        var value = PropertyOf(typeof(PersonModel), nameof(PersonModel.Name)).GetValue(person);

        Assert.Equal("Ada", value);
    }

    [Fact]
    public void SetValue_WritesThroughShapeSetter()
    {
        var person = new PersonModel();

        PropertyOf(typeof(PersonModel), nameof(PersonModel.Name)).SetValue(person, "Ada");
        PropertyOf(typeof(PersonModel), nameof(PersonModel.Age)).SetValue(person, 36);

        Assert.Equal("Ada", person.Name);
        Assert.Equal(36, person.Age);
    }

    [Fact]
    public void SetValue_NullForNonNullableValueTypeThrows()
    {
        var person = new PersonModel();

        Assert.Throws<ArgumentNullException>(() => PropertyOf(typeof(PersonModel), nameof(PersonModel.Age)).SetValue(person, null));
    }

    [Fact]
    public void SetValue_IncompatibleValueThrows()
    {
        var person = new PersonModel();

        Assert.Throws<ArgumentException>(() => PropertyOf(typeof(PersonModel), nameof(PersonModel.Age)).SetValue(person, "thirty-six"));
    }

    [Fact]
    public void SetValue_OnReadOnlyPropertyThrows()
    {
        var property = PropertyOf(typeof(PersonModel), nameof(PersonModel.ReadOnlyValue));

        Assert.True(property.IsReadOnly);
        Assert.False(property.CanResetValue(new PersonModel()));
        Assert.Throws<InvalidOperationException>(() => property.SetValue(new PersonModel(), "changed"));
    }

    [Fact]
    public void SetValue_NullComponentThrowsBeforeReadOnlyCheck()
    {
        var property = PropertyOf(typeof(PersonModel), nameof(PersonModel.ReadOnlyValue));

        Assert.Throws<ArgumentNullException>(() => property.SetValue(null, "changed"));
    }

    [Fact]
    public void GetValue_NullComponentThrows()
    {
        Assert.Throws<ArgumentNullException>(() => PropertyOf(typeof(PersonModel), nameof(PersonModel.Name)).GetValue(null));
    }

    [Fact]
    public void ResetValue_NullComponentThrows()
    {
        var age = PropertyOf(typeof(PersonModel), nameof(PersonModel.Age));

        Assert.Throws<ArgumentNullException>(() => age.ResetValue(null!));
    }

    [Fact]
    public void CanResetValue_NullComponent_ReturnsFalse()
    {
        var age = PropertyOf(typeof(PersonModel), nameof(PersonModel.Age));

        Assert.False(age.CanResetValue(null!));
    }

    [Fact]
    public void ShouldSerializeValue_NullComponent_ReturnsFalse()
    {
        var age = PropertyOf(typeof(PersonModel), nameof(PersonModel.Age));

        Assert.False(age.ShouldSerializeValue(null!));
    }

    [Fact]
    public void GetValue_OnBoxedStructReadsValue()
    {
        object boxed = new PointModel { X = 1, Y = 2 };

        var x = PropertyOf(typeof(PointModel), nameof(PointModel.X)).GetValue(boxed);

        Assert.Equal(1, x);
    }

    [Fact]
    public void SetValue_OnBoxedStructThrowsNotSupported()
    {
        object boxed = new PointModel { X = 1, Y = 2 };

        Assert.Throws<NotSupportedException>(() => PropertyOf(typeof(PointModel), nameof(PointModel.X)).SetValue(boxed, 5));
        Assert.Equal(1, ((PointModel)boxed).X);
    }

    [Fact]
    public void GetChildProperties_UsesShapeForTypeMetadata()
    {
        var address = PropertyOf(typeof(PersonModel), nameof(PersonModel.Address));

        var children = address.GetChildProperties();

        Assert.Equal(
            [nameof(AddressModel.City), nameof(AddressModel.PostalCode)],
            children.Cast<PropertyDescriptor>().Select(property => property.Name));
        Assert.Equal("The city name.", children[nameof(AddressModel.City)]!.Description);
    }

    [Fact]
    public void GetChildProperties_FiltersByAttribute()
    {
        var address = PropertyOf(typeof(PersonModel), nameof(PersonModel.Address));

        var children = address.GetChildProperties(null, [new DescriptionAttribute("The city name.")]);

        var child = Assert.Single(children.Cast<PropertyDescriptor>());
        Assert.Equal(nameof(AddressModel.City), child.Name);
    }

    [Fact]
    public void PropertyMetadata_ComesFromShapeAttributes()
    {
        var name = PropertyOf(typeof(PersonModel), nameof(PersonModel.Name));

        Assert.Equal("Full name", name.DisplayName);
        Assert.Equal("Identity", name.Category);
        Assert.Equal("The display name.", name.Description);
        Assert.True(name.IsBrowsable);
        Assert.False(name.DesignTimeOnly);
    }

    [Fact]
    public void IsReadOnly_HonorsReadOnlyAttribute()
    {
        var lockedProperty = PropertyOf(typeof(PersonModel), nameof(PersonModel.Locked));

        Assert.True(lockedProperty.IsReadOnly);
        Assert.Throws<InvalidOperationException>(() => lockedProperty.SetValue(new PersonModel(), "changed"));
    }

    [Fact]
    public void CanResetValue_UsesDefaultValueAttribute()
    {
        var age = PropertyOf(typeof(PersonModel), nameof(PersonModel.Age));
        var person = new PersonModel { Age = 36 };

        Assert.True(age.CanResetValue(person));
        Assert.True(age.ShouldSerializeValue(person));
        Assert.False(age.CanResetValue(new PersonModel { Age = 30 }));

        age.ResetValue(person);

        Assert.Equal(30, person.Age);
        Assert.False(age.CanResetValue(person));
        Assert.False(age.ShouldSerializeValue(person));
    }

    [Fact]
    public void SetValue_RaisesValueChangedHandlers()
    {
        var name = PropertyOf(typeof(PersonModel), nameof(PersonModel.Name));
        var person = new PersonModel();
        var raised = 0;
        EventHandler handler = (_, _) => raised++;

        name.AddValueChanged(person, handler);
        name.SetValue(person, "Ada");
        name.RemoveValueChanged(person, handler);
        name.SetValue(person, "Grace");

        Assert.Equal(1, raised);
    }

    [Fact]
    public void SupportsChangeEvents_ReflectsComponentContract()
    {
        var personProperty = PropertyOf(typeof(PersonModel), nameof(PersonModel.Name));
        var observableProperty = Provider.GetTypeDescriptor(typeof(ObservableModel), null)!
            .GetProperties()[nameof(ObservableModel.Value)]!;

        Assert.False(personProperty.SupportsChangeEvents);
        Assert.True(observableProperty.SupportsChangeEvents);
    }

    [Fact]
    public void Converter_FallsBackToTypeDescriptor()
    {
        var name = PropertyOf(typeof(PersonModel), nameof(PersonModel.Name));

        Assert.IsType<StringConverter>(name.Converter);
    }

    [Fact]
    public void Converter_UsesTypeConverterAttribute()
    {
        var code = PropertyOf(typeof(PersonModel), nameof(PersonModel.Code));

        var converter = Assert.IsType<UpperCaseConverter>(code.Converter);

        Assert.Equal("ABC", converter.ConvertFromInvariantString("abc"));
    }

    private static PropertyDescriptor PropertyOf(Type type, string name) =>
        Provider.GetTypeDescriptor(type, null)!.GetProperties()[name]!;
}