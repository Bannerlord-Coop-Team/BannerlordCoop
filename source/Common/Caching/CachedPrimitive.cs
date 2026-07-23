namespace Common.Caching;

public sealed class CachedPrimitive<T>
{
    public T Value { get; set; }

    public CachedPrimitive(T value)
    {
        Value = value;
    }
}
