using Mirai.Space.Extensions.MVVM.LifeTime;
using Xunit;

namespace Mirai.Space.Extensions.MVVM.Tests;

public sealed class AsyncLifeTimeTests
{
    [Fact]
    public async Task DisposeAsync_DisposesSyncAndAsyncItemsInReverseOrder()
    {
        var order = new List<string>();
        var lifeTime = new AsyncLifeTime();
        lifeTime.Add(new RecordingDisposable("sync", order));
        lifeTime.Add(new RecordingAsyncDisposable("async", order));

        await ((IAsyncDisposable)lifeTime).DisposeAsync();

        Assert.Equal(new[] { "async", "sync" }, order);
        Assert.True(lifeTime.IsDisposed);
    }

    [Fact]
    public async Task DisposeAsync_IsIdempotent()
    {
        var order = new List<string>();
        var lifeTime = new AsyncLifeTime();
        lifeTime.Add(new RecordingAsyncDisposable("async", order));

        await ((IAsyncDisposable)lifeTime).DisposeAsync();
        await ((IAsyncDisposable)lifeTime).DisposeAsync();

        Assert.Equal(new[] { "async" }, order);
    }

    [Fact]
    public async Task DisposeAsync_CancelsTokenBeforeDisposingItems()
    {
        var lifeTime = new AsyncLifeTime();
        var token = lifeTime.Token;
        var cancelledBeforeDispose = false;
        lifeTime.Add(new CallbackAsyncDisposable(() => cancelledBeforeDispose = token.IsCancellationRequested));

        await ((IAsyncDisposable)lifeTime).DisposeAsync();

        Assert.True(cancelledBeforeDispose);
    }

    [Fact]
    public void Dispose_WithAsyncOnlyItem_ThrowsAndExplainsAsyncDisposal()
    {
        var lifeTime = new AsyncLifeTime();
        lifeTime.Add(new RecordingAsyncDisposable("async", []));

        var exception = Assert.Throws<InvalidOperationException>(() => ((IDisposable)lifeTime).Dispose());

        Assert.Contains("dispose the owning AsyncLifeTime asynchronously", exception.Message);
        Assert.True(lifeTime.IsDisposed);
    }

    [Fact]
    public void Dispose_DisposesDisposableItems()
    {
        var order = new List<string>();
        var lifeTime = new AsyncLifeTime();
        lifeTime.Add(new RecordingDisposable("item", order));

        ((IDisposable)lifeTime).Dispose();

        Assert.Equal(new[] { "item" }, order);
    }

    [Fact]
    public async Task Add_AfterDisposeAsync_ThrowsObjectDisposedException()
    {
        var lifeTime = new AsyncLifeTime();
        await ((IAsyncDisposable)lifeTime).DisposeAsync();

        Assert.Throws<ObjectDisposedException>(() => lifeTime.Add(new RecordingAsyncDisposable("late", [])));
    }

    private sealed class RecordingDisposable(string name, List<string> order) : IDisposable
    {
        public void Dispose() => order.Add(name);
    }

    private sealed class RecordingAsyncDisposable(string name, List<string> order) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            order.Add(name);
            return default;
        }
    }

    private sealed class CallbackAsyncDisposable(Action onDispose) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            onDispose();
            return default;
        }
    }
}
