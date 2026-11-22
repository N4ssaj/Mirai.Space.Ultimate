using System;
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
        public IHttpClientBuilder UseJson()
        {
            ArgumentNullException.ThrowIfNull(builder);

            return builder.UseJson(_ => { });
        }

        public IHttpClientBuilder UseJson(Action<JsonSerializerOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(builder);

            var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            configure(options);
            return builder.UseSerializer(new JsonRequestSerializer(options));
        }

        public IHttpClientBuilder UseJson(JsonSerializerOptions options)
        {
            ArgumentNullException.ThrowIfNull(builder);

            return builder.UseSerializer(new JsonRequestSerializer(options));
        }

        public IHttpClientBuilder UseJson(JsonSerializerContext context)
        {
            ArgumentNullException.ThrowIfNull(builder);

            return builder.UseSerializer(new JsonRequestSerializer(context));
        }
    }

    extension(IServiceCollection services)
    {
        public IServiceCollection AddJsonSerializer()
        {
            ArgumentNullException.ThrowIfNull(services);

            return services.AddJsonSerializer(_ => { });
        }

        public IServiceCollection AddJsonSerializer(Action<JsonSerializerOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(services);

            var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            configure(options);
            services.AddSingleton<IRequestSerializer>(new JsonRequestSerializer(options));
            return services;
        }

        public IServiceCollection AddJsonSerializer(JsonSerializerOptions options)
        {
            ArgumentNullException.ThrowIfNull(services);

            services.AddSingleton<IRequestSerializer>(new JsonRequestSerializer(options));
            return services;
        }

        public IServiceCollection AddJsonSerializer(JsonSerializerContext context)
        {
            ArgumentNullException.ThrowIfNull(services);

            services.AddSingleton<IRequestSerializer>(new JsonRequestSerializer(context));
            return services;
        }

        public IServiceCollection AddProblemDetailsErrorHandler()
        {
            ArgumentNullException.ThrowIfNull(services);

            services.AddSingleton<IApiErrorHandler>(new ProblemDetailsErrorHandler());
            return services;
        }

        public IServiceCollection AddProblemDetailsErrorHandler(JsonSerializerOptions options)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(options);

            services.AddSingleton<IApiErrorHandler>(new ProblemDetailsErrorHandler(options));
            return services;
        }
    }
}
