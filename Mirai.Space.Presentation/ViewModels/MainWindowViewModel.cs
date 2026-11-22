namespace Mirai.Space.Presentation.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    public MainViewModel Main { get; }

    public MainWindowViewModel(MainViewModel main)
    {
        Main = main;
    }
}
