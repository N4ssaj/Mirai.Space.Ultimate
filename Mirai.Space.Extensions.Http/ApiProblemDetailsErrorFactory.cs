using System.Text.Json;
using Mirai.Space.Extensions.Http.Abstractions;

namespace Mirai.Space.Extensions.Http;

internal sealed class ApiProblemDetailsErrorFactory : IApiErrorFactory
{
    private const int MaxBodyLength = 8192;
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public async ValueTask<ApiException> CreateAsync(
        HttpResponseMessage response,
        CancellationToken ct = default)
    {
        var request = response.RequestMessage;
        var body = await ReadBodyAsync(response, ct).ConfigureAwait(false);

        ApiProblemDetails? problem = null;
        if (body is not null)
        {
            try
            {
                problem = JsonSerializer.Deserialize<ApiProblemDetails>(body, Options);
            }
            catch (JsonException)
            {
                // A non-JSON problem body still travels to callers in ResponseBody.
            }
        }

        var message = problem?.Title
            ?? $"HTTP {(int)response.StatusCode} {response.ReasonPhrase} for {request?.Method} {request?.RequestUri}.";

        return new ApiException(
            message,
            response.StatusCode,
            request?.Method,
            request?.RequestUri,
            problem,
            body);
    }

    private static async ValueTask<string?> ReadBodyAsync(
        HttpResponseMessage response,
        CancellationToken ct)
    {
        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (!string.Equals(mediaType, "application/problem+json", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(mediaType, "application/json", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var reader = new StreamReader(stream, leaveOpen: true, detectEncodingFromByteOrderMarks: true);
        var buffer = new char[MaxBodyLength];
        var read = await reader.ReadBlockAsync(buffer, ct).ConfigureAwait(false);
        return read == 0 ? null : new string(buffer, 0, read);
    }
}
