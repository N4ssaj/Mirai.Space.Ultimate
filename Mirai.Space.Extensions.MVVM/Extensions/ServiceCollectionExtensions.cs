using System;
using LifeTimeType = Mirai.Space.Extensions.MVVM.LifeTime.LifeTime;
using Mirai.Space.Extensions.MVVM.DependencyInjection;
using Mirai.Space.Extensions.MVVM.LifeTime;
using Microsoft.Extensions.DependencyInjection;

namespace Mirai.Space.Extensions.MVVM.Extensions;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddMvvmExtensions()
        {
            ArgumentNullException.ThrowIfNull(services);

            services.AddLazy();
            services.AddLifeTime();

            return services;
        }

        public IServiceCollection AddLifeTime()
        {
            ArgumentNullException.ThrowIfNull(services);

            services.AddTransient<LifeTimeType>();
            services.AddTransient<AsyncLifeTime>();

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
