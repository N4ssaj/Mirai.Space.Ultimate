using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Mirai.Space.Extensions.Http.Abstractions;
using Mirai.Space.Extensions.Http.Extensions;

namespace Mirai.Space.Extensions.Http.MessagePack.Extensions;

public static class ServiceCollectionExtensions
{
    extension(IHttpClientBuilder builder)
    {
        public IHttpClientBuilder UseMessagePack()
        {
            ArgumentNullException.ThrowIfNull(builder);

            return builder.UseMessagePack(static _ => MessagePackSerializerOptions.Standard);
        }

        public IHttpClientBuilder UseMessagePack(
            Func<MessagePackSerializerOptions, MessagePackSerializerOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(configure);

            var options = configure(MessagePackSerializerOptions.Standard);
            ArgumentNullException.ThrowIfNull(options);

            builder.Services.AddKeyedTransient<IRequestSerializer>(
                builder.Name,
                (_, _) => new MessagePackRequestSerializer(options));

            return builder;
        }
    }
}
