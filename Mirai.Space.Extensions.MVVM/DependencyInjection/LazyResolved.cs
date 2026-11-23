using Microsoft.Extensions.DependencyInjection;

namespace Mirai.Space.Extensions.MVVM.DependencyInjection;

internal class LazyResolved<T>(IServiceProvider sp) : Lazy<T>(sp.GetRequiredService<T>) where T : notnull
{
}
