using System.Diagnostics.CodeAnalysis;

namespace Mirai.Space.Extensions.Http;

internal static class ThrowHelper
{
    [DoesNotReturn]
    public static void ThrowInvalidOperationException(string message) =>
        throw new InvalidOperationException(message);
}
