using System.ComponentModel;
using Xunit;

namespace Mirai.Space.Extensions.ComponentModel.Tests;

public sealed class PolyTypeEventDescriptorTests
{
    private static readonly PolyTypeTypeDescriptionProvider Provider = PolyTypeTypeDescriptionProvider.For<PersonModel, ComponentModelWitness>();

    [Fact]
    public void GetEvents_ExposesShapeEventWithHandlerType()
    {
        var descriptor = Provider.GetTypeDescriptor(typeof(EventfulModel), null)!;

        var @event = Assert.Single(descriptor.GetEvents().Cast<EventDescriptor>());

        Assert.Equal(nameof(EventfulModel.Changed), @event.Name);
        Assert.Equal(typeof(EventHandler), @event.EventType);
        Assert.Equal(typeof(EventfulModel), @event.ComponentType);
    }

    [Fact]
    public void AddAndRemoveEventHandler_AttachAndDetachDelegate()
    {
        var descriptor = Provider.GetTypeDescriptor(typeof(EventfulModel), null)!;
        var @event = Assert.Single(descriptor.GetEvents().Cast<EventDescriptor>());
        var model = new EventfulModel();
        var raised = 0;
        EventHandler handler = (_, _) => raised++;

        @event.AddEventHandler(model, handler);
        model.RaiseChanged();
        @event.RemoveEventHandler(model, handler);
        model.RaiseChanged();

        Assert.Equal(1, raised);
    }

    [Fact]
    public void AddEventHandler_WrongDelegateTypeThrows()
    {
        var descriptor = Provider.GetTypeDescriptor(typeof(EventfulModel), null)!;
        var @event = Assert.Single(descriptor.GetEvents().Cast<EventDescriptor>());

        Assert.Throws<ArgumentException>(() => @event.AddEventHandler(new EventfulModel(), new Action(() => { })));
    }

    [Fact]
    public void AddEventHandler_NullDelegateThrowsArgumentNullException()
    {
        var descriptor = Provider.GetTypeDescriptor(typeof(EventfulModel), null)!;
        var @event = Assert.Single(descriptor.GetEvents().Cast<EventDescriptor>());

        Assert.Throws<ArgumentNullException>(() => @event.AddEventHandler(new EventfulModel(), null!));
    }

    [Fact]
    public void GetEvents_FiltersByAttributes()
    {
        var descriptor = Provider.GetTypeDescriptor(typeof(EventfulModel), null)!;

        var events = descriptor.GetEvents([new DescriptionAttribute("Raised when the model changes.")]);

        var @event = Assert.Single(events.Cast<EventDescriptor>());
        Assert.Equal(nameof(EventfulModel.Changed), @event.Name);
        Assert.Equal("Raised when the model changes.", @event.Description);
    }

    [Fact]
    public void GetEvents_WithoutShapeEventsFallsBackToParent()
    {
        var provider = new PolyTypeTypeDescriptionProvider(Provider.ShapeProvider, TypeDescriptor.GetProvider(typeof(QuietEventModel)));
        var descriptor = provider.GetTypeDescriptor(typeof(QuietEventModel), null)!;

        var @event = Assert.Single(descriptor.GetEvents().Cast<EventDescriptor>());
        Assert.Equal(nameof(QuietEventModel.Changed), @event.Name);
    }

    [Fact]
    public void GetEvents_DefaultParentFallsBackToReflection()
    {
        var provider = new PolyTypeTypeDescriptionProvider(Provider.ShapeProvider);
        var descriptor = provider.GetTypeDescriptor(typeof(QuietEventModel), null)!;

        var @event = Assert.Single(descriptor.GetEvents().Cast<EventDescriptor>());
        Assert.Equal(nameof(QuietEventModel.Changed), @event.Name);
    }
}