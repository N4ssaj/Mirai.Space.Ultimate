namespace Mirai.Space.Extensions.MVVM.Foundation;

public abstract class ReactiveDialog<T> : ReactiveComponent
{
    private readonly TaskCompletionSource<T> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task<T> Completion => _completion.Task;

    public void Close(T result) => _completion.TrySetResult(result);
}
