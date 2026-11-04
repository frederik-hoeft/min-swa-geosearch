namespace GeoSearch.Web.Shared.Web;

/// <summary>Represents a heap-backed URI route or query key-value pair.</summary>
public readonly record struct RouteData
{
    public readonly string Key;
    public readonly string Value;

    private RouteData(string key, string value)
    {
        Key = key;
        Value = value;
    }

    public static RouteData Create(string key, string? value)
    {
        ArgumentNullException.ThrowIfNull(key);
        return new RouteData(key, value ?? string.Empty);
    }

    public static RouteData Create<TValue>(string key, TValue? value)
    {
        ArgumentNullException.ThrowIfNull(key);
        return new RouteData(key, value?.ToString() ?? string.Empty);
    }

    public RouteDataRef ToRef() => RouteDataRef.Create(Key, Value);
}
