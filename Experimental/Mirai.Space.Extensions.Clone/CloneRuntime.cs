using System.ComponentModel;
using System.Reflection;

namespace Mirai.Space.Extensions.Clone;

public static class CloneRuntime
{
    private static readonly Func<object, object> MemberwiseCloneDelegate = CreateMemberwiseClone();

    // Generated deep plans start with a memberwise copy before walking the declared members.
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static object MemberwiseCloneObject(object value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return MemberwiseCloneDelegate(value);
    }

    private static Func<object, object> CreateMemberwiseClone()
    {
        var method = typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic);
        if (method is null)
        {
            ThrowHelper.ThrowMemberwiseCloneUnavailable();
        }

        return method.CreateDelegate<Func<object, object>>();
    }
}
