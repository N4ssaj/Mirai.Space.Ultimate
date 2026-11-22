using System.Net.Http.Headers;
using Mirai.Space.Extensions.Http.Abstractions;

namespace Mirai.Space.Extensions.Http;

internal sealed class NoopApiAuthorizationProvider : IApiAuthorizationProvider
{
    public ValueTask<AuthenticationHeaderValue?> GetAuthorizationAsync(CancellationToken ct = default) =>
        ValueTask.FromResult<AuthenticationHeaderValue?>(null);
}
