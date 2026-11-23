namespace Mirai.Space.Extensions.MVVM.Foundation;

public abstract class ReactiveDialog : ReactiveComponent
{
    private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task Completion => _completion.Task;

    public void Close() => _completion.TrySetResult();
}
