using ReactiveUI.SourceGenerators;

namespace Mirai.Space.Presentation.ViewModels;

public sealed partial class MainViewModel : ViewModelBase
{
    [Reactive]
    public partial string Greeting { get; set; } = "Welcome to Mirai.Space!";
}
