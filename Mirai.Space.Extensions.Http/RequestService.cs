using System.Net;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using Mirai.Space.Extensions.Http.Abstractions;

namespace Mirai.Space.Extensions.Http;

internal sealed partial class RequestService : IRequestService
{
    private readonly HttpClient _httpClient;
    private readonly IRequestSerializer _serializer;
    private readonly IApiErrorHandler _errorHandler;

    internal RequestService(HttpClient httpClient, IRequestSerializer serializer, IApiErrorHandler errorHandler)
    {
        _httpClient = httpClient;
        _serializer = serializer;
        _errorHandler = errorHandler;
    }

    private Task<ApiResponse> SendCoreAsync(HttpRequestMessage request, CancellationToken ct) =>
        SendCoreAsync(request, disposeRequest: true, ct);

    private async Task<ApiResponse> SendCoreAsync(
        HttpRequestMessage request,
        bool disposeRequest,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            using var response = await SendResponseAsync(request, ct).ConfigureAwait(false);
            return ApiResponse.FromMessage(response);
        }
        finally
        {
            if (disposeRequest)
            {
                request.Dispose();
            }
        }
    }

    private Task<ApiResponse<T>> SendCoreAsync<T>(HttpRequestMessage request, CancellationToken ct) =>
        SendCoreAsync<T>(request, disposeRequest: true, ct);

    private async Task<ApiResponse<T>> SendCoreAsync<T>(
        HttpRequestMessage request,
        bool disposeRequest,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            using var response = await SendResponseAsync(request, ct).ConfigureAwait(false);
            var content = await ReadAsync<T>(response, ct).ConfigureAwait(false);
            return ApiResponse<T>.FromMessage(response, content);
        }
        finally
        {
            if (disposeRequest)
            {
                request.Dispose();
            }
        }
    }

    private async Task<ApiResponse> SendForStreamCoreAsync(
        HttpRequestMessage request,
        Stream destination,
        bool disposeRequest,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(destination);

        try
        {
            using var response = await SendResponseAsync(request, ct).ConfigureAwait(false);
            await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            await stream.CopyToAsync(destination, ct).ConfigureAwait(false);

            return ApiResponse.FromMessage(response);
        }
        finally
        {
            if (disposeRequest)
            {
                request.Dispose();
            }
        }
    }

    private async IAsyncEnumerable<T> SendAsyncEnumerableCoreAsync<T>(
        HttpRequestMessage request,
        bool disposeRequest,
        [EnumeratorCancellation] CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            using var response = await SendResponseAsync(request, ct).ConfigureAwait(false);
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
        finally
        {
            if (disposeRequest)
            {
                request.Dispose();
            }
        }
    }

    private async ValueTask<HttpResponseMessage> SendResponseAsync(
        HttpRequestMessage request,
        CancellationToken ct)
    {
        var response = await _httpClient
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);

        try
        {
            response.RequestMessage ??= request;
            await ThrowIfErrorAsync(response, ct).ConfigureAwait(false);
            return response;
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }

    private async ValueTask ThrowIfErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var exception = await _errorHandler.HandleAsync(response, ct).ConfigureAwait(false);
        if (exception is null)
        {
            ThrowHelper.ThrowInvalidOperationException(
                $"{_errorHandler.GetType().Name} returned a null ApiException.");
        }

        throw exception;
    }

    private async ValueTask<T?> ReadAsync<T>(HttpResponseMessage response, CancellationToken ct)
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
        return await _serializer.DeserializeAsync<T>(stream, ct).ConfigureAwait(false);
    }

    private HttpContent CreateContent<TBody>(TBody body)
    {
        var bytes = _serializer.Serialize(body);
        var content = new ReadOnlyMemoryContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(_serializer.ContentType);
        return content;
    }
}
