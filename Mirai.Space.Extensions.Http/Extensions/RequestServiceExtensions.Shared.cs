using System.Runtime.CompilerServices;
using Mirai.Space.Extensions.Http.Abstractions;

namespace Mirai.Space.Extensions.Http;

public static partial class RequestServiceExtensions
{
    private static HttpRequestMessage CreateRequest(HttpMethod method, string url, HttpContent? content = null)
    {
        ArgumentNullException.ThrowIfNull(url);
        return new HttpRequestMessage(method, url) { Content = content };
    }

    private static async IAsyncEnumerable<T> GetAsyncEnumerableCore<T>(
        IRequestService requests,
        string url,
        [EnumeratorCancellation] CancellationToken ct)
    {
        using var request = CreateRequest(HttpMethod.Get, url);
        await foreach (var item in requests.SendAsyncEnumerable<T>(request, ct).WithCancellation(ct))
        {
            yield return item;
        }
    }
}
