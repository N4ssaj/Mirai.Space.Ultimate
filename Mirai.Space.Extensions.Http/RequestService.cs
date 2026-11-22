using System.Net;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using Mirai.Space.Extensions.Http.Abstractions;

namespace Mirai.Space.Extensions.Http;

public sealed partial class RequestService : IRequestService
{
    private readonly HttpClient _httpClient;
    private readonly IApiSerializer _serializer;
    private readonly IApiErrorFactory _errorFactory;

    public RequestService(HttpClient httpClient, IApiSerializer serializer, IApiErrorFactory errorFactory)
    {
        _httpClient = httpClient;
        _serializer = serializer;
        _errorFactory = errorFactory;
    }

    private Task<ApiResponse> SendCoreAsync(HttpRequestMessage request, CancellationToken ct) =>
        SendCoreAsync(request, disposeRequest: true, ct);

    private async Task<ApiResponse> SendCoreAsync(
        HttpRequestMessage request,
        bool disposeRequest,
        CancellationToken ct)
    {
        try
        {
            using var response = await _httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
                .ConfigureAwait(false);

            response.RequestMessage ??= request;
            await ThrowIfErrorAsync(response, ct).ConfigureAwait(false);
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
        try
        {
            using var response = await _httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
                .ConfigureAwait(false);

            response.RequestMessage ??= request;
            await ThrowIfErrorAsync(response, ct).ConfigureAwait(false);
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

    private async Task<StreamedApiResponse> SendForStreamCoreAsync(
        HttpRequestMessage request,
        bool disposeRequest,
        CancellationToken ct)
    {
        HttpResponseMessage? response = null;
        try
        {
            response = await _httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
                .ConfigureAwait(false);

            response.RequestMessage ??= request;
            await ThrowIfErrorAsync(response, ct).ConfigureAwait(false);

            var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            var result = new StreamedApiResponse(response, stream);
            response = null;
            return result;
        }
        finally
        {
            if (disposeRequest)
            {
                request.Dispose();
            }

            response?.Dispose();
        }
    }

    private async IAsyncEnumerable<T> SendAsyncEnumerableCoreAsync<T>(
        HttpRequestMessage request,
        bool disposeRequest,
        [EnumeratorCancellation] CancellationToken ct)
    {
        try
        {
            using var response = await _httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
                .ConfigureAwait(false);

            response.RequestMessage ??= request;
            await ThrowIfErrorAsync(response, ct).ConfigureAwait(false);

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

    private async ValueTask ThrowIfErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var exception = await _errorFactory.CreateAsync(response, ct).ConfigureAwait(false);
        if (exception is null)
        {
            ThrowHelper.ThrowInvalidOperationException(
                $"{_errorFactory.GetType().Name} returned a null ApiException.");
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
