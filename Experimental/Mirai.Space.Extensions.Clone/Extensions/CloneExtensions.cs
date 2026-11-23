namespace Mirai.Space.Extensions.Clone;

public static class CloneExtensions
{
    extension<T>(T value)
    {
        public T ShallowClone() => Clone.Shallow(value);

        public T DeepClone() => Clone.Deep(value);
    }
}
