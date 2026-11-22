using Microsoft.Extensions.DependencyInjection;
using Mirai.Space.Extensions.MVVM.Extensions;
using Mirai.Space.Presentation.Extensions;

namespace Mirai.Space.Desktop.Extensions;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddDesktop()
        {
            return services
                .AddMvvmExtensions()
                .AddPresentation();
        }
    }
}
