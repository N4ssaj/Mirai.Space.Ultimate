namespace Mirai.Space.Extensions.MVVM.LifeTime;

public sealed class AsyncLifeTime : LifeTime, IAsyncDisposable
{
    public void Add(IAsyncDisposable disposable)
    {
        ArgumentNullException.ThrowIfNull(disposable);

        AddCore(disposable);
    }

    ValueTask IAsyncDisposable.DisposeAsync()
    {
        var toDispose = BeginDispose();

        return toDispose is null ? default : DisposeAsyncCore(toDispose);
    }

    private async ValueTask DisposeAsyncCore(List<object> toDispose)
    {
        try
        {
            Cancellation.Cancel();

            for (int i = toDispose.Count - 1; i >= 0; i--)
            {
                if (toDispose[i] is IAsyncDisposable asyncDisposable)
                {
                    await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                }
                else
                {
                    ((IDisposable)toDispose[i]).Dispose();
                }
            }
        }
        finally
        {
            Cancellation.Dispose();
        }
    }
}
