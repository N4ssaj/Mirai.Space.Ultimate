using System.Net.Http.Headers;

namespace Mirai.Space.Extensions.Http.Abstractions;

public interface IApiAuthorizationProvider
{
    ValueTask<AuthenticationHeaderValue?> GetAuthorizationAsync(CancellationToken ct = default);
}
