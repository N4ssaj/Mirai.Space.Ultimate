using System.Net;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using Mirai.Space.Extensions.Http.Abstractions;

namespace Mirai.Space.Extensions.Http;

internal sealed partial class RequestService : IRequestService
{
    private readonly HttpClient _httpClient;
    private readonly IRequestSerializer _serializer;
    private readonly IRequestErrorHandler _errorHandler;

    internal RequestService(
        HttpClient httpClient,
        IRequestSerializer serializer,
        IRequestErrorHandler errorHandler)
    {
        _httpClient = httpClient;
        _serializer = serializer;
        _errorHandler = errorHandler;
    }

    private async Task<RequestResponse> SendResponseCoreAsync(
        HttpRequestMessage request,
        CancellationToken ct)
    {
        using var response = await _httpClient
            .SendAsync(request, HttpCompletionOption.ResponseContentRead, ct)
            .ConfigureAwait(false);

        await ValidateResponseAsync(response, request, ct).ConfigureAwait(false);

        return response.ToRequestResponse();
    }

    private async Task<RequestResponse<T>> SendResponseCoreAsync<T>(
        HttpRequestMessage request,
        CancellationToken ct)
    {
        using var response = await _httpClient
            .SendAsync(request, HttpCompletionOption.ResponseContentRead, ct)
            .ConfigureAwait(false);

        await ValidateResponseAsync(response, request, ct).ConfigureAwait(false);

        var content = await ReadResponseAsync<T>(response, ct).ConfigureAwait(false);

        return response.ToRequestResponse(content);
    }

    private async Task<T?> SendContentCoreAsync<T>(
        HttpRequestMessage request,
        CancellationToken ct)
    {
        using var response = await _httpClient
            .SendAsync(request, HttpCompletionOption.ResponseContentRead, ct)
            .ConfigureAwait(false);

        await ValidateResponseAsync(response, request, ct).ConfigureAwait(false);

        return await ReadResponseAsync<T>(response, ct).ConfigureAwait(false);
    }

    private async Task<RequestResponse> SendStreamCoreAsync(
        HttpRequestMessage request,
        Stream destination,
        CancellationToken ct)
    {
        using var response = await _httpClient
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);

        await ValidateResponseAsync(response, request, ct).ConfigureAwait(false);

        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        await stream.CopyToAsync(destination, ct).ConfigureAwait(false);

        return response.ToRequestResponse();
    }

    private async IAsyncEnumerable<T> SendAsyncEnumerableCoreAsync<T>(
        HttpRequestMessage request,
        [EnumeratorCancellation] CancellationToken ct)
    {
        using var response = await _httpClient
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);

        await ValidateResponseAsync(response, request, ct).ConfigureAwait(false);

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

    private static HttpRequestMessage CreateRequest(
        HttpMethod method,
        string url)
    {
        ArgumentNullException.ThrowIfNull(url);
        return new(method, url);
    }

    private HttpRequestMessage CreateRequestWithBody<TBody>(HttpMethod method, string url, TBody body)
    {
        var content = new ReadOnlyMemoryContent(_serializer.Serialize(body));
        content.Headers.ContentType = new MediaTypeHeaderValue(_serializer.ContentType);

        var request = CreateRequest(method, url);
        request.Content = content;

        return request;
    }
}
