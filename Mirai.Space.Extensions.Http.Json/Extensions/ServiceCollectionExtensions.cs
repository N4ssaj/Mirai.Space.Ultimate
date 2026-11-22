using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Mirai.Space.Extensions.Http.Abstractions;
using Mirai.Space.Extensions.Http.Extensions;

namespace Mirai.Space.Extensions.Http.Json.Extensions;

public static class ServiceCollectionExtensions
{
    extension(IHttpClientBuilder builder)
    {
        public IHttpClientBuilder UseJson() => builder.UseJson(_ => { });

        public IHttpClientBuilder UseJson(Action<JsonSerializerOptions> configure)
        {
            var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            configure(options);
            return builder.UseSerializer(new JsonApiSerializer(options));
        }

        public IHttpClientBuilder UseJson(JsonSerializerOptions options) =>
            builder.UseSerializer(new JsonApiSerializer(options));

        public IHttpClientBuilder UseJson(JsonSerializerContext context) =>
            builder.UseSerializer(new JsonApiSerializer(context));
    }

    extension(IServiceCollection services)
    {
        public IServiceCollection AddJsonSerializer() => services.AddJsonSerializer(_ => { });

        public IServiceCollection AddJsonSerializer(Action<JsonSerializerOptions> configure)
        {
            var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            configure(options);
            services.AddSingleton<IApiSerializer>(new JsonApiSerializer(options));
            return services;
        }

        public IServiceCollection AddJsonSerializer(JsonSerializerOptions options)
        {
            services.AddSingleton<IApiSerializer>(new JsonApiSerializer(options));
            return services;
        }

        public IServiceCollection AddJsonSerializer(JsonSerializerContext context)
        {
            services.AddSingleton<IApiSerializer>(new JsonApiSerializer(context));
            return services;
        }
    }
}
