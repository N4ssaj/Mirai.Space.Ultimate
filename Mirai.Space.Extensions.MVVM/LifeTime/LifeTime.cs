namespace Mirai.Space.Extensions.MVVM.LifeTime;

public class LifeTime : IDisposable
{
    protected readonly List<object> Disposables = [];
    protected readonly Lock Gate = new();
    protected readonly CancellationTokenSource Cancellation = new();
    private bool _disposed;

    public bool IsDisposed => Volatile.Read(ref _disposed);

    public CancellationToken Token => Cancellation.Token;

    public void Add(IDisposable disposable)
    {
        ArgumentNullException.ThrowIfNull(disposable);

        AddCore(disposable);
    }

    void IDisposable.Dispose() => DisposeCore();

    protected void AddCore(object disposable)
    {
        lock (Gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            Disposables.Add(disposable);
        }
    }

    protected List<object>? BeginDispose()
    {
        lock (Gate)
        {
            if (_disposed)
            {
                return null;
            }

            Volatile.Write(ref _disposed, true);

            return Disposables;
        }
    }

    protected void DisposeCore()
    {
        var toDispose = BeginDispose();

        if (toDispose is null)
        {
            return;
        }

        try
        {
            Cancellation.Cancel();

            for (int i = toDispose.Count - 1; i >= 0; i--)
            {
                if (toDispose[i] is IDisposable disposable)
                {
                    disposable.Dispose();
                }
                else
                {
                    ThrowHelper.ThrowAsyncDisposableRequiresAsyncDispose(toDispose[i]);
                }
            }
        }
        finally
        {
            Cancellation.Dispose();
        }
    }
}
