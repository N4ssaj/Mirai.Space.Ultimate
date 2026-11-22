using Mirai.Space.Extensions.Http.Abstractions;

namespace Mirai.Space.Extensions.Http;

/// <summary>
/// Default <see cref="IApiReauthenticator"/>: never retries. Replace it with the application's
/// login flow (UI thread, token refresh, reconnect) to enable 401 -> retry in the pipeline.
/// </summary>
public sealed class NoopApiReauthenticator : IApiReauthenticator
{
    public Task<bool> TryReauthenticateAsync(CancellationToken ct = default) =>
        Task.FromResult(false);
}
