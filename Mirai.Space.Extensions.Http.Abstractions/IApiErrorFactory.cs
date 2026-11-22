namespace Mirai.Space.Extensions.Http.Abstractions;

public interface IApiErrorFactory
{
    ValueTask<ApiException> CreateAsync(HttpResponseMessage response, CancellationToken ct = default);
}
