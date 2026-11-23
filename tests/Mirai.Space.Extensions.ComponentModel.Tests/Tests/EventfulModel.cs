using System.ComponentModel;
using PolyType;

namespace Mirai.Space.Extensions.ComponentModel.Tests;

[TypeShape(IncludeMethods = MethodShapeFlags.PublicInstance)]
[DefaultEvent(nameof(Changed))]
public sealed class EventfulModel
{
    [Description("Raised when the model changes.")]
    public event EventHandler? Changed;

    public void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}