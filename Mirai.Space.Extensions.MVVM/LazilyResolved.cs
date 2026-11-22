using Microsoft.Extensions.DependencyInjection;

namespace Mirai.Space.Extensions.MVVM;

internal sealed class LazilyResolved<T>(IServiceProvider serviceProvider)
    : Lazy<T>(() => serviceProvider.GetRequiredService<T>())
    where T : notnull;
