using System;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
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

            return AddRequestServiceClient<TClient>(services);
        }

        public IHttpClientBuilder AddRequestService()
        {
            ArgumentNullException.ThrowIfNull(services);

            return AddRequestServiceCore(services, null);
        }
    }

    private static IHttpClientBuilder AddRequestServiceClient<TClient>(IServiceCollection services)
        where TClient : class
    {
        var name = typeof(TClient).FullName!;
        var builder = AddRequestServiceCore(services, name);

        services.TryAddTransient<TClient>(sp =>
            ActivatorUtilities.CreateInstance<TClient>(
                sp,
                sp.GetRequiredKeyedService<IRequestService>(name)));

        return builder;
    }

    private static IHttpClientBuilder AddRequestServiceCore(
        IServiceCollection services,
        string? name)
    {
        services.AddOptions<RequestServiceOptions>();
        services.TryAddSingleton<IRequestSerializer>(
            _ => new JsonRequestSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        services.TryAddSingleton<IRequestErrorHandler, DefaultRequestErrorHandler>();

        var builder = services.AddHttpClient(name ?? string.Empty);
        var key = builder.Name;

        builder.ConfigureHttpClient((sp, client) =>
        {
            var serializer = sp.GetKeyedService<IRequestSerializer>(key)
                ?? sp.GetRequiredService<IRequestSerializer>();

            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue(serializer.AcceptMediaType));
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
        var options = sp.GetRequiredService<IOptionsMonitor<RequestServiceOptions>>().Get(key);

        return new RequestService(httpClient, serializer, errorHandler, options);
    }
}
