namespace Mirai.Space.Extensions.Http.Abstractions;

public interface IApiReauthenticator
{
    Task<bool> TryReauthenticateAsync(CancellationToken ct = default);
}
