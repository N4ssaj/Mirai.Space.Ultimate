using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Mirai.Space.Extensions.MVVM.Extensions;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddMvvmExtensions()
        {
            services.AddLazySingletons();

            return services;
        }

        public IServiceCollection AddLazySingletons()
        {
            services.TryAddSingleton(typeof(Lazy<>), typeof(LazilyResolved<>));

            return services;
        }
    }
}
