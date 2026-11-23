using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace Mirai.Space.Extensions.Clone;

[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class CloneContext
{
    private readonly Dictionary<object, object> _references = new(ReferenceEqualityComparer.Instance);

    public bool TryGetReference(object source, [NotNullWhen(true)] out object? clone) =>
        _references.TryGetValue(source, out clone);

    public void RegisterReference(object source, object clone) => _references[source] = clone;

    public T Clone<T>(T value) => value is null ? value : CloneCache<T>.Plan.Deep(value, this);
}
