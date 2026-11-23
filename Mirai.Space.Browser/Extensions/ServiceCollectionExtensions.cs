using System;
using Microsoft.Extensions.DependencyInjection;

namespace Mirai.Space.Application.Extensions;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddApplication()
        {
            ArgumentNullException.ThrowIfNull(services);

            return services;
        }
    }
}
