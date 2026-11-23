using LifeTimeType = Mirai.Space.Extensions.MVVM.LifeTime.LifeTime;
using Microsoft.Extensions.DependencyInjection;
using Mirai.Space.Extensions.MVVM.Extensions;
using Mirai.Space.Extensions.MVVM.LifeTime;
using Xunit;

namespace Mirai.Space.Extensions.MVVM.Tests;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddLifeTime_RegistersTransientLifeTime()
    {
        var provider = new ServiceCollection().AddLifeTime().BuildServiceProvider();

        Assert.NotSame(
            provider.GetRequiredService<LifeTimeType>(),
            provider.GetRequiredService<LifeTimeType>());
    }

    [Fact]
    public void AddLifeTime_RegistersTransientAsyncLifeTime()
    {
        var provider = new ServiceCollection().AddLifeTime().BuildServiceProvider();

        Assert.NotSame(
            provider.GetRequiredService<AsyncLifeTime>(),
            provider.GetRequiredService<AsyncLifeTime>());
    }

    [Fact]
    public void AddMvvmExtensions_RegistersLazyResolution()
    {
        var provider = new ServiceCollection().AddMvvmExtensions().BuildServiceProvider();

        var lazy = provider.GetRequiredService<Lazy<IServiceScopeFactory>>();

        Assert.Same(provider.GetRequiredService<IServiceScopeFactory>(), lazy.Value);
    }

    [Fact]
    public void AddMvvmExtensions_NullServices_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => ((IServiceCollection)null!).AddMvvmExtensions());
    }
}
