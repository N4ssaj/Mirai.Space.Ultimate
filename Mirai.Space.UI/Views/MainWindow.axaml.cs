using Mirai.Space.Presentation.ViewModels;
using ReactiveUI.Avalonia;

namespace Mirai.Space.UI.Views;

public partial class MainWindow : ReactiveWindow<MainWindowViewModel>
{
    public MainWindow()
    {
        InitializeComponent();
    }
}
