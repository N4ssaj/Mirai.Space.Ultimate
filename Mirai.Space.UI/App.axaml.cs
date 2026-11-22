using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Mirai.Space.Presentation.ViewModels;
using Mirai.Space.UI.Views;

// The Mirai.Space.Application namespace shadows Avalonia's Application type inside this namespace.
using AvaloniaApplication = Avalonia.Application;

namespace Mirai.Space.UI;

public partial class App : AvaloniaApplication
{
    private readonly Lazy<MainWindowViewModel> _mainWindowViewModel;
    private readonly Lazy<MainViewModel> _mainViewModel;

    public App(Lazy<MainWindowViewModel> mainWindowViewModel, Lazy<MainViewModel> mainViewModel)
    {
        _mainWindowViewModel = mainWindowViewModel;
        _mainViewModel = mainViewModel;
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow
            {
                DataContext = _mainWindowViewModel.Value
            };

            desktop.MainWindow = window;
            window.Show();
        }

        if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            singleViewPlatform.MainView = new MainView
            {
                DataContext = _mainViewModel.Value
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
