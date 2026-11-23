using System.Diagnostics.CodeAnalysis;

namespace Mirai.Space.Extensions.MVVM;

internal static class ThrowHelper
{
    [DoesNotReturn]
    internal static void ThrowAsyncDisposableRequiresAsyncDispose(object disposable)
        => throw new InvalidOperationException(
            $"{disposable.GetType().FullName} implements IAsyncDisposable but not IDisposable; dispose the owning AsyncLifeTime asynchronously.");
}
