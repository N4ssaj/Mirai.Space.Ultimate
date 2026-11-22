using System.Text.Json;
using Mirai.Space.Extensions.Http.Abstractions;

namespace Mirai.Space.Extensions.Http.Json;

internal sealed class ProblemDetailsErrorHandler : IApiErrorHandler
{
    private const int DefaultMaxBodyLength = 8192;
    private static readonly JsonSerializerOptions DefaultOptions = new(JsonSerializerDefaults.Web);
    private readonly JsonSerializerOptions _options;
    private readonly int _maxBodyLength;

    public ProblemDetailsErrorHandler(
        JsonSerializerOptions? options = null,
        int maxBodyLength = DefaultMaxBodyLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBodyLength);

        _options = options ?? DefaultOptions;
        _maxBodyLength = maxBodyLength;
    }

    public async ValueTask<ApiException> HandleAsync(
        HttpResponseMessage response,
        CancellationToken ct = default)
    {
        var request = response.RequestMessage;
        var body = await ReadBodyAsync(response, ct).ConfigureAwait(false);

        RequestProblemDetails? problem = null;
        if (body is not null)
        {
            try
            {
                problem = JsonSerializer.Deserialize<RequestProblemDetails>(body, _options);
            }
            catch (JsonException)
            {
                // The body is not ProblemDetails; fall back to the status line.
            }
        }

        var message = problem?.Title
            ?? $"HTTP {(int)response.StatusCode} {response.ReasonPhrase} for {request?.Method} {request?.RequestUri}.";

        return new ApiException(
            message,
            response.StatusCode,
            request?.Method,
            request?.RequestUri,
            problem);
    }

    private async ValueTask<string?> ReadBodyAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (!string.Equals(mediaType, "application/problem+json", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(mediaType, "application/json", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var reader = new StreamReader(stream, leaveOpen: true, detectEncodingFromByteOrderMarks: true);
        var buffer = new char[_maxBodyLength + 1];
        var read = await reader.ReadBlockAsync(buffer, ct).ConfigureAwait(false);
        return read == 0 || read > _maxBodyLength ? null : new string(buffer, 0, read);
    }
}
