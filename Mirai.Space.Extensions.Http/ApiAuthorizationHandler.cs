using Mirai.Space.Extensions.Http.Abstractions;

namespace Mirai.Space.Extensions.Http;

public sealed class ApiAuthorizationHandler : DelegatingHandler
{
    private readonly IApiAuthorizationProvider _provider;

    public ApiAuthorizationHandler(IApiAuthorizationProvider provider)
    {
        _provider = provider;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken ct)
    {
        var authorization = await _provider.GetAuthorizationAsync(ct).ConfigureAwait(false);
        request.Headers.Authorization = authorization;
        return await base.SendAsync(request, ct).ConfigureAwait(false);
    }
}
