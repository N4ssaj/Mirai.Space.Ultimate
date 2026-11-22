using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Mirai.Space.Extensions.MVVM.Extensions;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddMvvmExtensions()
        {
            ArgumentNullException.ThrowIfNull(services);

            services.AddLazy();

            return services;
        }

        public IServiceCollection AddLazy()
        {
            ArgumentNullException.ThrowIfNull(services);

            services.AddTransient(typeof(Lazy<>), typeof(LazyResolved<>));
            services.AddSingleton(typeof(Lazy<>), typeof(LazyResolved<>));
            services.AddScoped(typeof(Lazy<>), typeof(LazyResolved<>));

            return services;
        }
    }
}
