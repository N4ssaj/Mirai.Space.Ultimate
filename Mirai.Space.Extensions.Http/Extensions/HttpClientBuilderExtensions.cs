using System;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Mirai.Space.Extensions.Http.Abstractions;
using Mirai.Space.Extensions.Http.Json;

namespace Mirai.Space.Extensions.Http.Extensions;

public static class HttpClientBuilderExtensions
{
    extension(IHttpClientBuilder builder)
    {
        public IHttpClientBuilder ConfigureRequestService(Action<RequestServiceOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(configure);

            builder.Services.Configure<RequestServiceOptions>(builder.Name, configure);
            return builder;
        }

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
