using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Mirai.Space.Extensions.Http.Abstractions;
using Mirai.Space.Extensions.Http.Extensions;

namespace Mirai.Space.Extensions.Http.MessagePack.Extensions;

public static class ServiceCollectionExtensions
{
    extension(IHttpClientBuilder builder)
    {
        public IHttpClientBuilder UseMessagePack(MessagePackSerializerOptions options) =>
            builder.UseSerializer(new MessagePackApiSerializer(options));

        public IHttpClientBuilder UseMessagePack(
            Func<MessagePackSerializerOptions, MessagePackSerializerOptions> configure) =>
            builder.UseMessagePack(configure(MessagePackSerializerOptions.Standard));
    }

    extension(IServiceCollection services)
    {
        public IServiceCollection AddMessagePackSerializer(MessagePackSerializerOptions options)
        {
            services.AddSingleton<IApiSerializer>(new MessagePackApiSerializer(options));
            return services;
        }

        public IServiceCollection AddMessagePackSerializer(
            Func<MessagePackSerializerOptions, MessagePackSerializerOptions> configure) =>
            services.AddMessagePackSerializer(configure(MessagePackSerializerOptions.Standard));
    }
}
