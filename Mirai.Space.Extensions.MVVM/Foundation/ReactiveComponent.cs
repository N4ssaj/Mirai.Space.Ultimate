using ReactiveUI;

namespace Mirai.Space.Extensions.MVVM.Foundation;

public abstract class ReactiveComponent : ReactiveObject, IActivatableViewModel
{
    public ViewModelActivator Activator { get; } = new();
}
