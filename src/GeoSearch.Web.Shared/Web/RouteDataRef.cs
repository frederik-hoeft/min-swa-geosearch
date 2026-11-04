namespace GeoSearch.Web.Shared.Web;

/// <summary>Represents a span-backed URI route or query key-value pair.</summary>
public readonly ref struct RouteDataRef
{
    public readonly ReadOnlySpan<char> Key;
    public readonly ReadOnlySpan<char> Value;

    private RouteDataRef(ReadOnlySpan<char> key, ReadOnlySpan<char> value)
    {
        Key = key;
        Value = value;
    }

    public RouteData CreateDeepCopy() => RouteData.Create(Key.ToString(), Value.ToString());

    public static RouteDataRef Create(ReadOnlySpan<char> key, ReadOnlySpan<char> value) => new(key, value);

    public static RouteDataRef Create<TValue>(ReadOnlySpan<char> key, TValue? value) => new(key, value?.ToString() ?? string.Empty);

}
