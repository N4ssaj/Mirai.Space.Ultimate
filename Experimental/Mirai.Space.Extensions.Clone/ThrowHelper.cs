using System.Diagnostics.CodeAnalysis;

namespace Mirai.Space.Extensions.Clone;

internal static class ThrowHelper
{
    [DoesNotReturn]
    internal static void ThrowMemberwiseCloneUnavailable() =>
        throw new InvalidOperationException("System.Object.MemberwiseClone could not be resolved.");
}
