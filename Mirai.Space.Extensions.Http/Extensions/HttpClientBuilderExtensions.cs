using System;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Mirai.Space.Extensions.Http.Abstractions;

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

            builder.Services.Configure<JsonSerializerOptions>(builder.Name, configure);
            return builder;
        }

        public IHttpClientBuilder UseSerializer<TSerializer>()
            where TSerializer : class, IRequestSerializer
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.Services.AddKeyedTransient<IRequestSerializer, TSerializer>(builder.Name);
            return builder;
        }

        public IHttpClientBuilder UseErrorHandler<THandler>()
            where THandler : class, IRequestErrorHandler
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.Services.AddKeyedTransient<IRequestErrorHandler, THandler>(builder.Name);
            return builder;
        }
    }

}
