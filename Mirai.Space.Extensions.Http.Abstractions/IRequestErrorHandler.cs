namespace Mirai.Space.Extensions.Http.Abstractions;

public interface IRequestErrorHandler
{
    ValueTask HandleAsync(RequestException exception, CancellationToken ct = default);
}
