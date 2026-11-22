using System;
using Microsoft.Extensions.DependencyInjection;

namespace Mirai.Space.Core.Extensions;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddCore()
        {
            ArgumentNullException.ThrowIfNull(services);

            return services;
        }
    }
}
