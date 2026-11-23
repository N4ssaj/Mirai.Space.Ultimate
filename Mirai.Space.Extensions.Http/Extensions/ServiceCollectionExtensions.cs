using System;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Mirai.Space.Extensions.Http;
using Mirai.Space.Extensions.Http.Abstractions;
using Mirai.Space.Extensions.Http.Json;

namespace Mirai.Space.Extensions.Http.Extensions;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IHttpClientBuilder AddRequestService<TClient>()
            where TClient : class
        {
            ArgumentNullException.ThrowIfNull(services);

            var name = typeof(TClient).FullName!;
            var builder = AddRequestServiceCore(services, name);

            services.TryAddTransient<TClient>(sp =>
                ActivatorUtilities.CreateInstance<TClient>(
                    sp,
                    sp.GetRequiredKeyedService<IRequestService>(name)));

            return builder;
        }

        public IHttpClientBuilder AddRequestService()
        {
            ArgumentNullException.ThrowIfNull(services);

            return AddRequestServiceCore(services, null);
        }
    }

    private static IHttpClientBuilder AddRequestServiceCore(
        IServiceCollection services,
        string? name)
    {
        services.TryAddSingleton<IRequestSerializer, JsonRequestSerializer>();
        services.TryAddSingleton<IRequestErrorHandler, DefaultRequestErrorHandler>();

        var builder = services.AddHttpClient(name ?? string.Empty);
        var key = builder.Name;

        builder.ConfigureHttpClient((sp, client) =>
        {
            var serializer = sp.GetKeyedService<IRequestSerializer>(key)
                ?? sp.GetRequiredService<IRequestSerializer>();

            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue(serializer.ContentType));
        });

        services.TryAddKeyedTransient<IRequestService>(key, (sp, _) => CreateRequestService(sp, key));

        if (name is null)
        {
            services.TryAddTransient<IRequestService>(sp =>
                sp.GetRequiredKeyedService<IRequestService>(key));
        }

        return builder;
    }

    private static IRequestService CreateRequestService(IServiceProvider sp, string key)
    {
        var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient(key);
        var serializer = sp.GetKeyedService<IRequestSerializer>(key)
            ?? sp.GetRequiredService<IRequestSerializer>();
        var errorHandler = sp.GetKeyedService<IRequestErrorHandler>(key)
            ?? sp.GetRequiredService<IRequestErrorHandler>();

        return new RequestService(httpClient, serializer, errorHandler);
    }

    extension(IHttpClientBuilder builder)
    {
        public IHttpClientBuilder ConfigureJson(Action<JsonSerializerOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(configure);

            var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            configure(options);
            return builder.UseSerializer(new JsonRequestSerializer(options));
        }

        public IHttpClientBuilder ConfigureJson(JsonSerializerOptions options)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(options);

            return builder.UseSerializer(new JsonRequestSerializer(options));
        }

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
            where THandler : class, IRequestErrorHandler
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.Services.TryAddKeyedSingleton<IRequestErrorHandler, THandler>(builder.Name);
            return builder;
        }

        public IHttpClientBuilder UseErrorHandler(IRequestErrorHandler errorHandler)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(errorHandler);

            builder.Services.TryAddKeyedSingleton(builder.Name, errorHandler);
            return builder;
        }
    }
}
