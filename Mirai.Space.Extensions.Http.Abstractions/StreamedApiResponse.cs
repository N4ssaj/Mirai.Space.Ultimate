namespace Mirai.Space.Extensions.Http.Abstractions;

public sealed class StreamedApiResponse : ApiResponse, IDisposable, IAsyncDisposable
{
    private readonly HttpResponseMessage _response;
    private readonly Stream _content;
    private int _disposed;

    public Stream Content
    {
        get
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
            return _content;
        }
    }

    public StreamedApiResponse(HttpResponseMessage response, Stream content)
        : base(response.StatusCode, Snapshot(response.Headers), Snapshot(response.Content.Headers))
    {
        _response = response;
        _content = content;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _content.Dispose();
        _response.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        await _content.DisposeAsync().ConfigureAwait(false);
        _response.Dispose();
    }
}
