using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Mirai.Space.Extensions.Http.Abstractions;

namespace Mirai.Space.Extensions.Http.Handlers;

public sealed class ApiReauthenticationHandler : DelegatingHandler
{
    private readonly IApiReauthenticator _reauthenticator;
    private readonly ILogger<ApiReauthenticationHandler> _logger;

    public ApiReauthenticationHandler(
        IApiReauthenticator reauthenticator,
        ILogger<ApiReauthenticationHandler>? logger = null)
    {
        _reauthenticator = reauthenticator;
        _logger = logger ?? NullLogger<ApiReauthenticationHandler>.Instance;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken ct)
    {
        var response = await base.SendAsync(request, ct).ConfigureAwait(false);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }

        if (!IsReplayable(request.Content))
        {
            _logger.LogWarning(
                "Skipping re-authentication for {Method} {Uri}: the request body is not replayable.",
                request.Method,
                request.RequestUri);
            return response;
        }

        if (!await _reauthenticator.TryReauthenticateAsync(ct).ConfigureAwait(false))
        {
            return response;
        }

        response.Dispose();

        using var retry = Clone(request);
        return await base.SendAsync(retry, ct).ConfigureAwait(false);
    }

    private static bool IsReplayable(HttpContent? content) =>
        content is null
            or ByteArrayContent
            or StringContent
            or FormUrlEncodedContent
            or ReadOnlyMemoryContent;

    private static HttpRequestMessage Clone(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version,
            VersionPolicy = request.VersionPolicy,
            Content = request.Content,
        };

        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        foreach (var option in request.Options)
        {
            clone.Options.Set(new HttpRequestOptionsKey<object?>(option.Key), option.Value);
        }

        return clone;
    }
}
