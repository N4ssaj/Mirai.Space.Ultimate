using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Mirai.Space.Extensions.Http.Json;

internal static class ThrowHelper
{
    [DoesNotReturn]
    public static void ThrowJsonException(string message) =>
        throw new JsonException(message);
}
