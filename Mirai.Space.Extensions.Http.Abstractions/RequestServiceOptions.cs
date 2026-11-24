using System.Net.Http;

namespace Mirai.Space.Extensions.Http.Abstractions;

public sealed class RequestServiceOptions
{
    /// <summary>
    /// Gets or sets the completion option applied to buffered sends; streaming sends always use
    /// <see cref="HttpCompletionOption.ResponseHeadersRead"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="HttpCompletionOption.ResponseContentRead"/> buffers the whole response body for buffered calls only;
    /// streaming calls ignore this option and always use <see cref="HttpCompletionOption.ResponseHeadersRead"/>.
    /// </remarks>
    public HttpCompletionOption CompletionOption { get; set; } = HttpCompletionOption.ResponseHeadersRead;
}
