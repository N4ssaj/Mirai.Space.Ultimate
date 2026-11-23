namespace Mirai.Space.Extensions.Clone;

public static class Clone
{
    public static T Shallow<T>(T value) => value is null ? value : CloneCache<T>.Plan.Shallow(value);

    public static T Deep<T>(T value) => value is null ? value : CloneCache<T>.Plan.Deep(value, new CloneContext());

    public static CloneStrategy GetStrategy<T>() => CloneCache<T>.Strategy;
}
