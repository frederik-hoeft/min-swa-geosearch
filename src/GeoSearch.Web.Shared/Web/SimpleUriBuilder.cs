using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace GeoSearch.Web.Shared.Web;

/// <summary>Builds trusted relative or absolute URI strings from path and query components.</summary>
/// <remarks>Path components are treated as URI syntax. Query keys and values are encoded by default.</remarks>
[DebuggerDisplay("{Build(),nq}")]
public sealed class SimpleUriBuilder
{
    private const int DEFAULT_CAPACITY = 256;
    private readonly StringBuilder _builder;
    private bool _hasQuery;
    private bool _queryIsEmpty;

    private SimpleUriBuilder(StringBuilder builder, bool hasQuery, bool queryIsEmpty)
    {
        _builder = builder;
        _hasQuery = hasQuery;
        _queryIsEmpty = queryIsEmpty;
    }

    public static SimpleUriBuilder Create(ReadOnlySpan<char> baseUri, int capacity = DEFAULT_CAPACITY)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        StringBuilder builder = new(Math.Max(capacity, baseUri.Length));
        builder.Append(baseUri);
        int queryIndex = baseUri.IndexOf('?');
        bool hasQuery = queryIndex >= 0;
        bool queryIsEmpty = hasQuery && queryIndex == baseUri.Length - 1;
        return new SimpleUriBuilder(builder, hasQuery, queryIsEmpty);
    }

    public SimpleUriBuilder AppendPath(ReadOnlySpan<char> path)
    {
        if (_hasQuery)
        {
            throw new InvalidOperationException("A path cannot be appended after a query string has started.");
        }
        if (path.IsEmpty)
        {
            return this;
        }
        bool builderHasDelimiter = _builder.Length > 0 && _builder[^1] == '/';
        bool pathHasDelimiter = path[0] == '/';
        if (builderHasDelimiter && pathHasDelimiter)
        {
            _builder.Append(path[1..]);
        }
        else if (!builderHasDelimiter && !pathHasDelimiter)
        {
            _builder.Append('/').Append(path);
        }
        else
        {
            _builder.Append(path);
        }
        return this;
    }

    public SimpleUriBuilder AppendQuery<TValue>(ReadOnlySpan<char> key, TValue? value, bool urlEncode = true)
    {
        string formatted = value switch
        {
            null => string.Empty,
            IFormattable formattable => formattable.ToString(format: null, CultureInfo.InvariantCulture) ?? string.Empty,
            _ => value.ToString() ?? string.Empty
        };
        return AppendQuery(key, formatted.AsSpan(), urlEncode);
    }

    public SimpleUriBuilder AppendQuery(ReadOnlySpan<char> key, bool value, bool urlEncode = true) =>
        AppendQuery(key, (value ? "true" : "false").AsSpan(), urlEncode);

    public SimpleUriBuilder AppendQuery(RouteDataRef routeData, bool urlEncode = true) =>
        AppendQuery(routeData.Key, routeData.Value, urlEncode);

    public SimpleUriBuilder AppendQuery(RouteData routeData, bool urlEncode = true) =>
        AppendQuery(routeData.Key.AsSpan(), routeData.Value.AsSpan(), urlEncode);

    public SimpleUriBuilder AppendQuery(ReadOnlySpan<char> key, ReadOnlySpan<char> value, bool urlEncode = true)
    {
        if (key.IsEmpty || key.IsWhiteSpace())
        {
            throw new ArgumentException("Query parameter keys cannot be empty or whitespace.", nameof(key));
        }
        AppendQueryDelimiter();
        if (urlEncode)
        {
            _builder.Append(Uri.EscapeDataString(key));
            _builder.Append('=');
            _builder.Append(Uri.EscapeDataString(value));
        }
        else
        {
            _builder.Append(key);
            _builder.Append('=');
            _builder.Append(value);
        }
        _queryIsEmpty = false;
        return this;
    }

    public string Build() => _builder.ToString();

    public Uri BuildUri(UriKind uriKind) => new(Build(), uriKind);

    public override string ToString() => Build();

    private void AppendQueryDelimiter()
    {
        if (!_hasQuery)
        {
            _builder.Append('?');
            _hasQuery = true;
            _queryIsEmpty = true;
            return;
        }
        if (!_queryIsEmpty && _builder.Length > 0 && _builder[^1] != '&')
        {
            _builder.Append('&');
        }
    }
}
