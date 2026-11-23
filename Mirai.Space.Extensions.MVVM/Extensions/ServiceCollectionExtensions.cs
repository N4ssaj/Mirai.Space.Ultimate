using System;
using LifeTimeType = Mirai.Space.Extensions.MVVM.LifeTime.LifeTime;
using Mirai.Space.Extensions.MVVM.DependencyInjection;
using Mirai.Space.Extensions.MVVM.LifeTime;
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
            services.AddLifeTime();

            return services;
        }

        public IServiceCollection AddLifeTime()
        {
            ArgumentNullException.ThrowIfNull(services);

            services.TryAddTransient<LifeTimeType>();
            services.TryAddTransient<AsyncLifeTime>();

            return services;
        }

        public IServiceCollection AddLazy()
        {
            ArgumentNullException.ThrowIfNull(services);

            services.TryAddTransient(typeof(Lazy<>), typeof(LazyResolved<>));

            return services;
        }
    }
}
