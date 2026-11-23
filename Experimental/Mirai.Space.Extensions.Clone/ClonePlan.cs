using System.ComponentModel;

namespace Mirai.Space.Extensions.Clone;

// Public because the generated registration lives in the consuming assembly.
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class ClonePlan<T>
{
    public ClonePlan(Func<T, T> shallow, Func<T, CloneContext, T> deep)
    {
        Shallow = shallow;
        Deep = deep;
    }

    public Func<T, T> Shallow { get; }

    public Func<T, CloneContext, T> Deep { get; }
}
