namespace Mirai.Space.Extensions.ComponentModel.Tests;

public sealed class QuietEventModel
{
    public event EventHandler? Changed;

    public void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}