using System.Net.Http;

namespace Mirai.Space.Extensions.Http.Abstractions;

public sealed class RequestServiceOptions
{
    // Applies to every send, including streaming: ResponseContentRead buffers the whole body.
    public HttpCompletionOption CompletionOption { get; set; } = HttpCompletionOption.ResponseHeadersRead;
}
