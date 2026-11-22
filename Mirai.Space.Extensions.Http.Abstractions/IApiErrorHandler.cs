namespace Mirai.Space.Extensions.Http.Abstractions;

public interface IApiErrorHandler
{
    ValueTask<ApiException> HandleAsync(HttpResponseMessage response, CancellationToken ct = default);
}
