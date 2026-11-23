using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Mirai.Space.Extensions.Http.Extensions;

namespace Mirai.Space.Extensions.Http.MessagePack.Extensions;

public static class ServiceCollectionExtensions
{
    extension(IHttpClientBuilder builder)
    {
        public IHttpClientBuilder UseMessagePack()
        {
            ArgumentNullException.ThrowIfNull(builder);

            return builder.UseMessagePack(MessagePackSerializerOptions.Standard);
        }

        public IHttpClientBuilder UseMessagePack(MessagePackSerializerOptions options)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(options);

            return builder.UseSerializer(new MessagePackRequestSerializer(options));
        }

        public IHttpClientBuilder UseMessagePack(
            Func<MessagePackSerializerOptions, MessagePackSerializerOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(configure);

            return builder.UseMessagePack(configure(MessagePackSerializerOptions.Standard));
        }
    }
}
