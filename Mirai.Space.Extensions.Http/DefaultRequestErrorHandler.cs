using Mirai.Space.Extensions.Http.Abstractions;

namespace Mirai.Space.Extensions.Http;

internal sealed class DefaultRequestErrorHandler : IRequestErrorHandler
{
    public ValueTask HandleAsync(HttpResponseMessage response, CancellationToken ct = default) =>
        ValueTask.CompletedTask;
}
