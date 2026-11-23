using LifeTimeType = Mirai.Space.Extensions.MVVM.LifeTime.LifeTime;
using Xunit;

namespace Mirai.Space.Extensions.MVVM.Tests;

public sealed class LifeTimeTests
{
    [Fact]
    public void Dispose_DisposesRegisteredItemsInReverseOrder()
    {
        var order = new List<string>();
        var lifeTime = new LifeTimeType();
        lifeTime.Add(new RecordingDisposable("first", order));
        lifeTime.Add(new RecordingDisposable("second", order));

        ((IDisposable)lifeTime).Dispose();

        Assert.Equal(new[] { "second", "first" }, order);
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var order = new List<string>();
        var lifeTime = new LifeTimeType();
        lifeTime.Add(new RecordingDisposable("item", order));

        ((IDisposable)lifeTime).Dispose();
        ((IDisposable)lifeTime).Dispose();

        Assert.Equal(new[] { "item" }, order);
        Assert.True(lifeTime.IsDisposed);
    }

    [Fact]
    public void Dispose_CancelsTokenBeforeDisposingItems()
    {
        var lifeTime = new LifeTimeType();
        var token = lifeTime.Token;
        var cancelledBeforeDispose = false;
        lifeTime.Add(new CallbackDisposable(() => cancelledBeforeDispose = token.IsCancellationRequested));

        ((IDisposable)lifeTime).Dispose();

        Assert.True(cancelledBeforeDispose);
    }

    [Fact]
    public void Token_IsNotCancelledBeforeDispose()
    {
        var lifeTime = new LifeTimeType();

        Assert.False(lifeTime.Token.IsCancellationRequested);
    }

    [Fact]
    public void Add_AfterDispose_ThrowsObjectDisposedException()
    {
        var lifeTime = new LifeTimeType();
        ((IDisposable)lifeTime).Dispose();

        Assert.Throws<ObjectDisposedException>(() => lifeTime.Add(new RecordingDisposable("late", [])));
    }

    [Fact]
    public void Add_Null_ThrowsArgumentNullException()
    {
        var lifeTime = new LifeTimeType();

        Assert.Throws<ArgumentNullException>(() => lifeTime.Add(null!));
    }

    private sealed class RecordingDisposable(string name, List<string> order) : IDisposable
    {
        public void Dispose() => order.Add(name);
    }

    private sealed class CallbackDisposable(Action onDispose) : IDisposable
    {
        public void Dispose() => onDispose();
    }
}
