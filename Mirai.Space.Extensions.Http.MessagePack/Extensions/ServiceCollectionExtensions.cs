using System;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Mirai.Space.Extensions.Http.Abstractions;
using Mirai.Space.Extensions.Http.Extensions;

namespace Mirai.Space.Extensions.Http.MessagePack.Extensions;

public static class ServiceCollectionExtensions
{
    extension(IHttpClientBuilder builder)
    {
        public IHttpClientBuilder UseMessagePack() =>
            builder.UseMessagePack(MessagePackSerializerOptions.Standard);

        public IHttpClientBuilder UseMessagePack(MessagePackSerializerOptions options)
        {
            ArgumentNullException.ThrowIfNull(builder);

            return builder.UseSerializer(new MessagePackRequestSerializer(options));
        }

        public IHttpClientBuilder UseMessagePack(
            Func<MessagePackSerializerOptions, MessagePackSerializerOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(builder);

            return builder.UseMessagePack(configure(MessagePackSerializerOptions.Standard));
        }
    }

    extension(IServiceCollection services)
    {
        public IServiceCollection AddMessagePackSerializer() =>
            services.AddMessagePackSerializer(MessagePackSerializerOptions.Standard);

        public IServiceCollection AddMessagePackSerializer(MessagePackSerializerOptions options)
        {
            ArgumentNullException.ThrowIfNull(services);

            services.AddSingleton<IRequestSerializer>(new MessagePackRequestSerializer(options));
            return services;
        }

        public IServiceCollection AddMessagePackSerializer(
            Func<MessagePackSerializerOptions, MessagePackSerializerOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(services);

            return services.AddMessagePackSerializer(configure(MessagePackSerializerOptions.Standard));
        }
    }
}
