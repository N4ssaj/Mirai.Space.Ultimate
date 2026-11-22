using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Mirai.Space.Extensions.Http.Abstractions;
using Mirai.Space.Extensions.Http.Handlers;

namespace Mirai.Space.Extensions.Http.Extensions;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddHttpExtensions()
        {
            services.TryAddSingleton<IApiErrorFactory, DefaultApiErrorFactory>();
            services.TryAddSingleton<IApiAuthorizationProvider, NoopApiAuthorizationProvider>();
            services.TryAddSingleton<IApiReauthenticator, NoopApiReauthenticator>();
            services.TryAddTransient<ApiAuthorizationHandler>();
            services.TryAddTransient<ApiReauthenticationHandler>();

            return services;
        }

        public IHttpClientBuilder AddRequestService<TClient>()
            where TClient : class
        {
            var name = typeof(TClient).FullName!;

            var builder = services.AddHttpClient(name)
                .AddHttpMessageHandler<ApiReauthenticationHandler>()
                .AddHttpMessageHandler<ApiAuthorizationHandler>();

            services.AddHttpExtensions();
            services.TryAddTransient<TClient>(sp =>
            {
                var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient(name);
                var serializer = sp.GetKeyedService<IApiSerializer>(name)
                    ?? sp.GetRequiredService<IApiSerializer>();
                var errorFactory = sp.GetKeyedService<IApiErrorFactory>(name)
                    ?? sp.GetRequiredService<IApiErrorFactory>();

                var requests = new RequestService(httpClient, serializer, errorFactory);
                return ActivatorUtilities.CreateInstance<TClient>(sp, requests);
            });

            return builder;
        }

        public IServiceCollection AddApiErrorFactory<TFactory>()
            where TFactory : class, IApiErrorFactory
        {
            services.AddSingleton<IApiErrorFactory, TFactory>();
            return services;
        }

        public IServiceCollection AddProblemDetailsErrorFactory()
        {
            services.AddSingleton<IApiErrorFactory, ApiProblemDetailsErrorFactory>();
            return services;
        }
    }

    extension(IHttpClientBuilder builder)
    {
        public IHttpClientBuilder UseSerializer<TSerializer>()
            where TSerializer : class, IApiSerializer
        {
            builder.Services.TryAddKeyedSingleton<IApiSerializer, TSerializer>(builder.Name);
            return builder;
        }

        public IHttpClientBuilder UseSerializer(IApiSerializer serializer)
        {
            builder.Services.TryAddKeyedSingleton(builder.Name, serializer);
            return builder;
        }

        public IHttpClientBuilder UseErrorFactory<TFactory>()
            where TFactory : class, IApiErrorFactory
        {
            builder.Services.TryAddKeyedSingleton<IApiErrorFactory, TFactory>(builder.Name);
            return builder;
        }
    }
}
