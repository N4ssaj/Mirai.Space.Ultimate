using System;
using System.Text.Json;
using System.Text.Json.Serialization;
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
        public IHttpClientBuilder AddRequestService()
        {
            ArgumentNullException.ThrowIfNull(services);

            var key = Options.DefaultName;
            var builder = AddRequestServiceCore(services, key);

            services.TryAddTransient<IRequestService>(sp =>
                sp.GetRequiredKeyedService<IRequestService>(key));

            return builder;
        }

        public IHttpClientBuilder AddRequestService(string name)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentException.ThrowIfNullOrWhiteSpace(name);

            return AddRequestServiceCore(services, name);
        }

        public IHttpClientBuilder AddRequestService<TClient>()
            where TClient : class
        {
            ArgumentNullException.ThrowIfNull(services);

            return services.AddRequestService<TClient>(typeof(TClient).FullName!);
        }

        public IHttpClientBuilder AddRequestService<TClient>(string name)
            where TClient : class
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentException.ThrowIfNullOrWhiteSpace(name);

            var builder = AddRequestServiceCore(services, name);

            services.TryAddTransient<TClient>(sp =>
                ActivatorUtilities.CreateInstance<TClient>(
                    sp,
                    sp.GetRequiredKeyedService<IRequestService>(name)));

            return builder;
        }
    }

    private static IHttpClientBuilder AddRequestServiceCore(
        IServiceCollection services,
        string key)
    {
        services.AddOptions<RequestServiceOptions>();
        services.Configure<JsonSerializerOptions>(key, ConfigureJsonDefaults);

        var builder = services.AddHttpClient(key);

        services.TryAddKeyedTransient<IRequestSerializer>(key,
            (sp, _) => new JsonRequestSerializer(
                sp.GetRequiredService<IOptionsFactory<JsonSerializerOptions>>().Create(key)));
        services.TryAddKeyedTransient<IRequestErrorHandler, DefaultRequestErrorHandler>(key);
        services.TryAddKeyedTransient<IRequestService>(key, (sp, _) => CreateRequestService(sp, key));

        return builder;
    }

    private static void ConfigureJsonDefaults(JsonSerializerOptions options)
    {
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.PropertyNameCaseInsensitive = true;
        options.NumberHandling = JsonNumberHandling.AllowReadingFromString;
    }

    private static IRequestService CreateRequestService(IServiceProvider sp, string key)
    {
        var options = sp.GetRequiredService<IOptionsFactory<RequestServiceOptions>>().Create(key);

        return new RequestService(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient(key),
            sp.GetRequiredKeyedService<IRequestSerializer>(key),
            sp.GetRequiredKeyedService<IRequestErrorHandler>(key),
            options);
    }
}
