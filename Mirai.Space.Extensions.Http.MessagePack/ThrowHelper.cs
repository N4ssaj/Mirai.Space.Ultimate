using System.Diagnostics.CodeAnalysis;

namespace Mirai.Space.Extensions.Http.MessagePack;

internal static class ThrowHelper
{
    [DoesNotReturn]
    public static void ThrowNotSupportedException(string message) =>
        throw new NotSupportedException(message);
}
