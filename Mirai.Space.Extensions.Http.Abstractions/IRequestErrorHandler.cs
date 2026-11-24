using System.Net.Http;

namespace Mirai.Space.Extensions.Http.Abstractions;

public interface IRequestErrorHandler
{
    /// <summary>Handles a non-success response before the default <see cref="RequestException"/> is thrown.</summary>
    /// <remarks>
    /// Throw to replace the default failure with a custom exception. Return normally to let
    /// <c>RequestService</c> throw the default exception; the failure cannot be suppressed.
    /// The response is disposed after this method returns, so it must not be stored.
    /// </remarks>
    ValueTask HandleAsync(HttpResponseMessage response, CancellationToken ct = default);
}
