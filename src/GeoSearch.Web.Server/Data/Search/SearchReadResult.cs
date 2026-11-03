namespace GeoSearch.Web.Server.Data.Search;

/// <summary>Database read result with the materialized view snapshot used for the features.</summary>
internal sealed record SearchReadResult(string Snapshot, IReadOnlyList<SearchFeatureRow> Features);
