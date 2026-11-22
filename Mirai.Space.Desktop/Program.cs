using System;
using Avalonia;
using Microsoft.Extensions.Hosting;
using Mirai.Space.Desktop.Extensions;
using Mirai.Space.UI;
using ReactiveMarbles.Extensions.Hosting.Avalonia;
using ReactiveUI.Avalonia;

namespace Mirai.Space.Desktop;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        builder.Services.AddDesktop();
        builder.ConfigureAvalonia(avalonia => avalonia
                .UseApplication(typeof(App))
                .ConfigureAppBuilder(appBuilder => appBuilder
                    .WithInterFont()
                    .UseReactiveUI(_ => { })))
            .UseAvaloniaLifetime();

        using var host = builder.Build();
        host.Run();
    }
}
