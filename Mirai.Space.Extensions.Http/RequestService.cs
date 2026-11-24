using System.Net;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using Mirai.Space.Extensions.Http.Abstractions;

namespace Mirai.Space.Extensions.Http;

internal sealed class RequestService : IRequestService
{
    private readonly HttpClient _httpClient;
    private readonly IRequestSerializer _serializer;
    private readonly IRequestErrorHandler _errorHandler;
    private readonly RequestServiceOptions _options;

    internal RequestService(
        HttpClient httpClient,
        IRequestSerializer serializer,
        IRequestErrorHandler errorHandler,
        RequestServiceOptions? options = null)
    {
        _httpClient = httpClient;
        _serializer = serializer;
        _errorHandler = errorHandler;
        _options = options ?? new RequestServiceOptions();
    }

    public async Task<RequestResponse> SendAsync(
        HttpRequestMessage message,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        using var response = await _httpClient
            .SendAsync(message, _options.CompletionOption, ct)
            .ConfigureAwait(false);

        await ValidateResponseAsync(response, message, ct).ConfigureAwait(false);

        return response.ToRequestResponse();
    }

    public async Task<RequestResponse<T>> SendAsync<T>(
        HttpRequestMessage message,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        using var response = await _httpClient
            .SendAsync(message, _options.CompletionOption, ct)
            .ConfigureAwait(false);

        await ValidateResponseAsync(response, message, ct).ConfigureAwait(false);

        var content = await ReadResponseAsync<T>(response, ct).ConfigureAwait(false);

        return response.ToRequestResponse(content);
    }

    public async Task<RequestResponse> SendToStreamAsync(
        HttpRequestMessage message,
        Stream destination,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(destination);

        using var response = await _httpClient
            .SendAsync(message, _options.CompletionOption, ct)
            .ConfigureAwait(false);

        await ValidateResponseAsync(response, message, ct).ConfigureAwait(false);

        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        await stream.CopyToAsync(destination, ct).ConfigureAwait(false);

        return response.ToRequestResponse();
    }

    public IAsyncEnumerable<T> SendAsyncEnumerable<T>(
        HttpRequestMessage message,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        return SendAsyncEnumerableIterator<T>(message, ct);
    }

    public HttpContent CreateContent<TBody>(TBody body)
    {
        var content = new ReadOnlyMemoryContent(_serializer.Serialize(body));
        content.Headers.ContentType = new MediaTypeHeaderValue(_serializer.ContentType);
        return content;
    }

    private async IAsyncEnumerable<T> SendAsyncEnumerableIterator<T>(
        HttpRequestMessage message,
        [EnumeratorCancellation] CancellationToken ct)
    {
        using var response = await _httpClient
            .SendAsync(message, _options.CompletionOption, ct)
            .ConfigureAwait(false);

        await ValidateResponseAsync(response, message, ct).ConfigureAwait(false);

        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        var contentType = response.Content.Headers.ContentType?.MediaType;

        await foreach (var item in _serializer
                           .DeserializeAsyncEnumerable<T>(stream, contentType, ct)
                           .WithCancellation(ct)
                           .ConfigureAwait(false))
        {
            yield return item;
        }
    }

    private async ValueTask ValidateResponseAsync(
        HttpResponseMessage response,
        HttpRequestMessage request,
        CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var exception = response.ToRequestException(request);
        await _errorHandler.HandleAsync(exception, ct).ConfigureAwait(false);
        throw exception;
    }

    private async ValueTask<T?> ReadResponseAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.ResetContent)
        {
            return default;
        }

        if (response.Content.Headers.ContentLength == 0)
        {
            return default;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        if (stream.CanSeek && stream.Length == 0)
        {
            return default;
        }

        return await _serializer.DeserializeAsync<T>(stream, ct).ConfigureAwait(false);
    }
}
