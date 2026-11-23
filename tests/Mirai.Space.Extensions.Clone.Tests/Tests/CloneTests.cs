using Mirai.Space.Extensions.Clone;
using Xunit;

namespace Mirai.Space.Extensions.Clone.Tests;

public static class CloneTests
{
    [Fact]
    public static void ShallowClone_SharesNestedReference()
    {
        var source = new Order
        {
            Number = "A-1",
            ShippingAddress = new Address { City = "Yekaterinburg" },
            Lines = [new OrderLine { Sku = "SKU-1", Quantity = 2 }],
            Metadata = { ["source"] = "test" },
        };

        var clone = Clone.Shallow(source);

        Assert.NotSame(source, clone);
        Assert.Same(source.ShippingAddress, clone.ShippingAddress);
        Assert.Same(source.Lines, clone.Lines);
        Assert.Same(source.Lines[0], clone.Lines[0]);
    }

    [Fact]
    public static void DeepClone_ClonesNestedObjectsAndCollections()
    {
        var source = new Order
        {
            Number = "A-1",
            ShippingAddress = new Address { City = "Yekaterinburg" },
            Lines = [new OrderLine { Sku = "SKU-1", Quantity = 2 }],
            Metadata = { ["source"] = "test" },
        };

        var clone = Clone.Deep(source);

        Assert.NotSame(source, clone);
        Assert.NotSame(source.ShippingAddress, clone.ShippingAddress);
        Assert.Equal(source.ShippingAddress!.City, clone.ShippingAddress!.City);
        Assert.NotSame(source.Lines, clone.Lines);
        Assert.NotSame(source.Lines[0], clone.Lines[0]);
        Assert.Equal(source.Lines[0].Sku, clone.Lines[0].Sku);
        Assert.NotSame(source.Metadata, clone.Metadata);
        Assert.Equal(source.Metadata["source"], clone.Metadata["source"]);
    }

    [Fact]
    public static void DeepClone_PreservesCycles()
    {
        var first = new Node { Name = "first" };
        var second = new Node { Name = "second" };
        first.Next = second;
        second.Next = first;

        var clone = Clone.Deep(first);

        Assert.NotSame(first, clone);
        Assert.NotSame(second, clone.Next);
        Assert.Same(clone, clone.Next!.Next);
        Assert.Equal("second", clone.Next.Name);
    }

    [Fact]
    public static void DeepClone_StructCopiesValueAndDeepClonesReferenceMembers()
    {
        var source = new Coordinate
        {
            X = 1,
            Y = 2,
            Address = new Address { City = "Moscow" },
        };

        var clone = Clone.Deep(source);

        Assert.Equal(source.X, clone.X);
        Assert.Equal(source.Y, clone.Y);
        Assert.NotSame(source.Address, clone.Address);
        Assert.Equal(source.Address!.City, clone.Address!.City);
    }

    [Fact]
    public static void DeepClone_PlainTypeUsesExpressionFallback()
    {
        var source = new PlainDto
        {
            Name = "root",
            Child = new PlainDto { Name = "child" },
        };

        var clone = Clone.Deep(source);

        Assert.Equal(CloneStrategy.Expression, Clone.GetStrategy<PlainDto>());
        Assert.NotSame(source, clone);
        Assert.NotSame(source.Child, clone.Child);
        Assert.Equal(source.Child!.Name, clone.Child!.Name);
    }

    [Fact]
    public static void DeepClone_ImmutableRecordFallsBackToExpression()
    {
        var source = new PolyRecord("name", new Address { City = "Kazan" });

        var clone = Clone.Deep(source);

        Assert.Equal(CloneStrategy.Expression, Clone.GetStrategy<PolyRecord>());
        Assert.NotSame(source, clone);
        Assert.NotSame(source.Address, clone.Address);
        Assert.Equal(source.Address.City, clone.Address.City);
    }

    [Fact]
    public static void DeepClone_PolyTypeTypeUsesPolyType()
    {
        var source = new PolyDto
        {
            Name = "name",
            Address = new Address { City = "Kazan" },
        };

        var clone = Clone.Deep(source);

        Assert.Equal(CloneStrategy.PolyType, Clone.GetStrategy<PolyDto>());
        Assert.NotSame(source, clone);
        Assert.NotSame(source.Address, clone.Address);
        Assert.Equal(source.Address!.City, clone.Address!.City);
    }

    [Fact]
    public static void PolyTypeTypeWithInitOnlyMemberFallsBackToExpression()
    {
        var source = new MixedDto
        {
            Mutable = new Address { City = "Tula" },
            Immutable = new Address { City = "Ryazan" },
        };

        var clone = Clone.Deep(source);

        Assert.Equal(CloneStrategy.Expression, Clone.GetStrategy<MixedDto>());
        Assert.NotSame(source, clone);
        Assert.NotSame(source.Mutable, clone.Mutable);
        Assert.NotSame(source.Immutable, clone.Immutable);
        Assert.Equal(source.Immutable.City, clone.Immutable.City);
    }

    [Fact]
    public static void PolyTypePlanHonorsCloneIgnore()
    {
        var shared = new Address { City = "Belgorod" };
        var source = new PolyIgnoredDto
        {
            Address = new Address { City = "Kursk" },
            Shared = shared,
        };

        var clone = Clone.Deep(source);

        Assert.Equal(CloneStrategy.PolyType, Clone.GetStrategy<PolyIgnoredDto>());
        Assert.NotSame(source.Address, clone.Address);
        Assert.Same(shared, clone.Shared);
    }

    [Fact]
    public static void CloneableTypeWithEventUsesGeneratedPlan()
    {
        var source = new EventDto { Address = new Address { City = "Vologda" } };

        var clone = Clone.Deep(source);

        Assert.Equal(CloneStrategy.SourceGenerated, Clone.GetStrategy<EventDto>());
        Assert.NotSame(source.Address, clone.Address);
        Assert.Equal(source.Address.City, clone.Address.City);
    }

    [Fact]
    public static void CloneableTypeUsesSourceGeneratedPlan()
    {
        Assert.Equal(CloneStrategy.SourceGenerated, Clone.GetStrategy<Order>());
    }

    [Fact]
    public static void RecordWithoutShapeFallsBackToExpression()
    {
        var source = new UnsupportedRecord("name");

        var clone = Clone.Deep(source);

        Assert.Equal(CloneStrategy.Expression, Clone.GetStrategy<UnsupportedRecord>());
        Assert.Equal(source, clone);
    }

    [Fact]
    public static void CloneableRecordWithInitPropertiesFallsBackToExpression()
    {
        var source = new AnnotatedRecord("name", new Address { City = "Voronezh" });

        var clone = Clone.Deep(source);

        Assert.Equal(CloneStrategy.Expression, Clone.GetStrategy<AnnotatedRecord>());
        Assert.NotSame(source, clone);
        Assert.NotSame(source.Address, clone.Address);
        Assert.Equal(source.Address.City, clone.Address.City);
    }

    [Fact]
    public static void GeneratedCloneMethod_UsesGeneratedPlan()
    {
        var source = new GeneratedMethodDto
        {
            Name = "root",
            Child = new GeneratedMethodDto { Name = "child" },
        };

        var shallow = source.Clone();
        var clone = source.DeepClone();

        Assert.Equal(CloneStrategy.SourceGenerated, Clone.GetStrategy<GeneratedMethodDto>());
        Assert.NotSame(source, shallow);
        Assert.Same(source.Child, shallow.Child);
        Assert.NotSame(source, clone);
        Assert.NotSame(source.Child, clone.Child);
        Assert.Equal(source.Child!.Name, clone.Child!.Name);
    }

    [Fact]
    public static void CloneIgnore_LeavesMemberShared()
    {
        var shared = new Address { City = "Perm" };
        var source = new IgnoredMemberDto { Name = "root", Shared = shared };

        var clone = Clone.Deep(source);

        Assert.NotSame(source, clone);
        Assert.Same(shared, clone.Shared);
    }

    [Fact]
    public static void DeepClone_ReadonlyPrivateFieldIsCloned()
    {
        var source = new ReadonlyFieldDto(new Address { City = "Omsk" });

        var clone = Clone.Deep(source);

        Assert.Equal(CloneStrategy.Expression, Clone.GetStrategy<ReadonlyFieldDto>());
        Assert.NotSame(source, clone);
        Assert.NotSame(source.Address, clone.Address);
        Assert.Equal(source.Address.City, clone.Address.City);
    }

    [Fact]
    public static void DeepClone_InterfaceDispatchUsesRuntimeType()
    {
        IAddressHolder source = new AddressHolder { Address = new Address { City = "Tyumen" } };

        var clone = Clone.Deep(source);

        Assert.Equal(CloneStrategy.RuntimeDispatch, Clone.GetStrategy<IAddressHolder>());
        Assert.NotSame(source, clone);
        Assert.NotSame(source.Address, clone.Address);
        Assert.Equal(source.Address.City, clone.Address.City);
    }

    [Fact]
    public static void DeepClone_ArrayClonesElements()
    {
        var source = new[] { new Address { City = "Sochi" } };

        var clone = Clone.Deep(source);

        Assert.NotSame(source, clone);
        Assert.NotSame(source[0], clone[0]);
        Assert.Equal(source[0].City, clone[0].City);
    }

    [Fact]
    public static void ShallowClone_ListSharesElements()
    {
        var item = new Address { City = "Samara" };
        var source = new List<Address> { item };

        var clone = Clone.Shallow(source);

        Assert.NotSame(source, clone);
        Assert.Same(item, clone[0]);
    }

    [Fact]
    public static void DeepClone_MultidimensionalArrayClonesElements()
    {
        var source = new[,] { { new Address { City = "Ufa" } } };

        var clone = Clone.Deep(source);

        Assert.NotSame(source, clone);
        Assert.NotSame(source[0, 0], clone[0, 0]);
        Assert.Equal(source[0, 0].City, clone[0, 0].City);
    }

    [Fact]
    public static void DeepClone_NonZeroBasedArrayKeepsBounds()
    {
        var source = Array.CreateInstance(typeof(Address), [2], [1]);
        source.SetValue(new Address { City = "Kirov" }, 1);

        var clone = Clone.Deep(source);

        Assert.NotSame(source, clone);
        Assert.Equal(1, clone.GetLowerBound(0));
        Assert.NotSame(source.GetValue(1), clone.GetValue(1));
    }
}
