using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace GeoSearch.Web.Shared.Web;

/// <summary>Parses trusted URI text into lightweight span-backed scheme, host, path and query views.</summary>
/// <remarks>Query values are raw URI text, not percent-decoded.</remarks>
[DebuggerDisplay("Schema = {Schema}, Host = {Host}, Path = {Path}, Query = {Query}")]
public readonly ref struct SimpleUriParser
{
    private const string SCHEMA_DELIMITER = "://";
    private readonly int _queryStartIndex;
    private readonly int _hostStartIndex;
    private readonly int _pathStartIndex;

    private SimpleUriParser(ReadOnlySpan<char> uri, int hostStartIndex, int pathStartIndex, int queryStartIndex)
    {
        Uri = uri;
        _hostStartIndex = hostStartIndex;
        _pathStartIndex = pathStartIndex;
        _queryStartIndex = queryStartIndex;
    }

    public ReadOnlySpan<char> Uri { get; }
    public ReadOnlySpan<char> Schema => Uri[.._hostStartIndex];
    public ReadOnlySpan<char> SchemaHost => Uri[..Math.Min(_pathStartIndex, _queryStartIndex)];
    public ReadOnlySpan<char> Host
    {
        get
        {
            int hostStartIndex = _hostStartIndex == 0 ? 0 : _hostStartIndex + SCHEMA_DELIMITER.Length;
            return Uri[hostStartIndex..Math.Min(_pathStartIndex, _queryStartIndex)];
        }
    }
    public ReadOnlySpan<char> SchemaHostPath => Uri[.._queryStartIndex];
    public ReadOnlySpan<char> Path => Uri[Math.Min(_pathStartIndex, _queryStartIndex).._queryStartIndex];
    public ReadOnlySpan<char> Query => Uri[Math.Min(_queryStartIndex + 1, Uri.Length)..];

    public static SimpleUriParser Parse(ReadOnlySpan<char> uri)
    {
        if (uri.IsEmpty || uri.IsWhiteSpace())
        {
            return default;
        }
        int queryStartIndex = uri.IndexOf('?');
        if (queryStartIndex < 0)
        {
            queryStartIndex = uri.Length;
        }
        int schemaDelimiterIndex = uri.IndexOf(SCHEMA_DELIMITER);
        int hostStartIndex = schemaDelimiterIndex;
        int probablePathStartIndex;
        if (schemaDelimiterIndex < 0)
        {
            hostStartIndex = 0;
            probablePathStartIndex = 0;
        }
        else
        {
            probablePathStartIndex = schemaDelimiterIndex + SCHEMA_DELIMITER.Length;
        }
        int relativePathStartIndex = uri[probablePathStartIndex..queryStartIndex].IndexOf('/');
        int pathStartIndex = relativePathStartIndex < 0 ? queryStartIndex : probablePathStartIndex + relativePathStartIndex;
        return new SimpleUriParser(uri, hostStartIndex, pathStartIndex, queryStartIndex);
    }

    public bool ContainsQueryParameter(ReadOnlySpan<char> key) => TryGetQueryParameter(key, out _);

    public bool TryGetQueryParameter(ReadOnlySpan<char> key, out RouteDataRef value)
    {
        foreach (RouteDataRef routeData in this)
        {
            if (routeData.Key.Equals(key, StringComparison.Ordinal))
            {
                value = routeData;
                return true;
            }
        }
        value = default;
        return false;
    }

    public Enumerator GetEnumerator() => new(Query);

    [SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Public nested enumerator is required by the allocation-free foreach pattern.")]
    public ref struct Enumerator
    {
        private readonly ReadOnlySpan<char> _query;
        private int _tupleStartIndex;
        private int _valueStartIndex;
        private int _tupleEndIndex;

        internal Enumerator(ReadOnlySpan<char> query)
        {
            _query = query;
            _tupleStartIndex = -1;
            _valueStartIndex = -1;
            _tupleEndIndex = -1;
        }
        public readonly RouteDataRef Current
        {
            get
            {
                ReadOnlySpan<char> key = _query[_tupleStartIndex..(_valueStartIndex - 1)];
                ReadOnlySpan<char> value = _query[_valueStartIndex..(_tupleEndIndex - 1)];
                return RouteDataRef.Create(key, value);
            }
        }
        public bool MoveNext()
        {
            if (_query.IsEmpty)
            {
                return false;
            }
            if (_tupleStartIndex < 0)
            {
                _tupleStartIndex = 0;
            }
            else if (_tupleEndIndex <= _query.Length)
            {
                _tupleStartIndex = _tupleEndIndex;
            }
            else
            {
                return false;
            }
            int relativeValueStartIndex = _query[_tupleStartIndex..].IndexOf('=');
            if (relativeValueStartIndex < 0)
            {
                return false;
            }
            _valueStartIndex = _tupleStartIndex + relativeValueStartIndex + 1;
            int relativeTupleEndIndex = _query[_valueStartIndex..].IndexOf('&');
            _tupleEndIndex = relativeTupleEndIndex < 0 ? _query.Length + 1 : _valueStartIndex + relativeTupleEndIndex + 1;
            return true;
        }
        public void Reset()
        {
            _tupleStartIndex = -1;
            _valueStartIndex = -1;
            _tupleEndIndex = -1;
        }
    }
}
