using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Mirai.Space.Extensions.Http.Abstractions;

namespace Mirai.Space.Extensions.Http.Extensions;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddHttpExtensions()
        {
            ArgumentNullException.ThrowIfNull(services);

            services.TryAddSingleton<IApiErrorHandler, DefaultApiErrorHandler>();
            return services;
        }

        public IHttpClientBuilder AddRequestService<TClient>()
            where TClient : class
        {
            ArgumentNullException.ThrowIfNull(services);

            var name = typeof(TClient).FullName!;

            var builder = services.AddHttpClient(name);
            services.AddHttpExtensions();
            services.TryAddTransient<TClient>(sp =>
            {
                var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient(name);
                var serializer = sp.GetKeyedService<IRequestSerializer>(name)
                    ?? sp.GetRequiredService<IRequestSerializer>();
                var errorHandler = sp.GetKeyedService<IApiErrorHandler>(name)
                    ?? sp.GetRequiredService<IApiErrorHandler>();

                var requests = new RequestService(httpClient, serializer, errorHandler);
                return ActivatorUtilities.CreateInstance<TClient>(sp, requests);
            });

            return builder;
        }

        public IServiceCollection AddApiErrorHandler<THandler>()
            where THandler : class, IApiErrorHandler
        {
            ArgumentNullException.ThrowIfNull(services);

            services.AddSingleton<IApiErrorHandler, THandler>();
            return services;
        }
    }

    extension(IHttpClientBuilder builder)
    {
        public IHttpClientBuilder UseSerializer<TSerializer>()
            where TSerializer : class, IRequestSerializer
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.Services.TryAddKeyedSingleton<IRequestSerializer, TSerializer>(builder.Name);
            return builder;
        }

        public IHttpClientBuilder UseSerializer(IRequestSerializer serializer)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(serializer);

            builder.Services.TryAddKeyedSingleton(builder.Name, serializer);
            return builder;
        }

        public IHttpClientBuilder UseErrorHandler<THandler>()
            where THandler : class, IApiErrorHandler
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.Services.TryAddKeyedSingleton<IApiErrorHandler, THandler>(builder.Name);
            return builder;
        }
    }
}
