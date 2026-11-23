using Mirai.Space.Extensions.Http.Abstractions;

namespace Mirai.Space.Extensions.Http;

internal sealed class DefaultRequestErrorHandler : IRequestErrorHandler
{
    public ValueTask HandleAsync(RequestException exception, CancellationToken ct = default) =>
        ValueTask.CompletedTask;
}
