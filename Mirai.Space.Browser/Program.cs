using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Browser;
using Microsoft.Extensions.DependencyInjection;
using Mirai.Space.Extensions.MVVM.Extensions;
using Mirai.Space.Presentation.Extensions;
using Mirai.Space.UI;
using ReactiveUI.Avalonia;

namespace Mirai.Space.Browser;

internal sealed partial class Program
{
    private static readonly Lazy<IServiceProvider> Services = new(CreateServices);

    private static Task Main(string[] args) => BuildAvaloniaApp()
        .WithInterFont()
        .UseReactiveUI(_ => { })
        .StartBrowserAppAsync("out");

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure(() => ActivatorUtilities.CreateInstance<App>(Services.Value));

    private static IServiceProvider CreateServices()
    {
        var services = new ServiceCollection();
        services.AddMvvmExtensions();
        services.AddPresentation();

        return services.BuildServiceProvider();
    }
}
