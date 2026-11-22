using Microsoft.Extensions.DependencyInjection;
using Mirai.Space.Presentation.ViewModels;

namespace Mirai.Space.Presentation.Extensions;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddPresentation()
        {
            services.AddSingleton<MainViewModel>();
            services.AddSingleton<MainWindowViewModel>();

            return services;
        }
    }
}
