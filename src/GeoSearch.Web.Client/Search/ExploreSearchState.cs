using GeoSearch.Web.Shared.GeoJson;

namespace GeoSearch.Web.Client.Search;

/// <summary>Typed commands; labels never determine what selecting a suggestion does.</summary>
internal abstract record SearchSuggestion
{
    public sealed record LiteralQuery(string Query) : SearchSuggestion;
    public sealed record Place(GeoJsonFeature Feature) : SearchSuggestion;
    public sealed record Category(string Value) : SearchSuggestion;
    public sealed record ExploreNearby : SearchSuggestion;
}

internal abstract record SearchLoadState
{
    public sealed record Loading : SearchLoadState;
    public sealed record Ready(GeoJsonFeatureCollection Collection) : SearchLoadState;
    public sealed record Failed(string Message) : SearchLoadState;
}

/// <summary>Only the committed panel is displayed below autocomplete; inspection preserves its return target.</summary>
internal abstract record SearchPanelState
{
    public sealed record Idle : SearchPanelState;
    public sealed record Forward(string Query, SearchLoadState Results, int Limit = 10) : SearchPanelState;
    public sealed record Place(GeoJsonFeature Feature, SearchPanelState ReturnTo, string ReturnText) : SearchPanelState;
}

/// <summary>Immutable presentation state. Draft edits never mutate the committed panel.</summary>
internal sealed record ExploreSearchState(
    string DisplayText,
    string CommittedText,
    bool IsEditing,
    int HighlightedIndex,
    SearchPanelState Panel)
{
    public static ExploreSearchState Initial { get; } = new("", "", false, -1, new SearchPanelState.Idle());

    public ExploreSearchState Edit(string text) =>
        this with { DisplayText = text, IsEditing = true, HighlightedIndex = -1 };

    public ExploreSearchState Highlight(int index) => this with { HighlightedIndex = index };

    public ExploreSearchState AbandonDraft() =>
        this with { DisplayText = CommittedText, IsEditing = false, HighlightedIndex = -1 };

    public ExploreSearchState CommitQuery(string query) =>
        this with { DisplayText = query, CommittedText = query, IsEditing = false, HighlightedIndex = -1, Panel = new SearchPanelState.Forward(query, new SearchLoadState.Loading()) };

    public ExploreSearchState CommitPlace(GeoJsonFeature feature) =>
        new(feature.Properties.Label, feature.Properties.Label, false, -1,
            new SearchPanelState.Place(feature, Panel, CommittedText));

    public ExploreSearchState RequestMore(string query, int limit) =>
        Panel is SearchPanelState.Forward current && current.Query == query
            ? this with { Panel = current with { Limit = limit, Results = new SearchLoadState.Loading() } }
            : this;

    public ExploreSearchState WithForwardResult(string query, SearchLoadState result) =>
        Panel is SearchPanelState.Forward current && current.Query == query
            ? this with { Panel = current with { Results = result } }
            : this;

    public ExploreSearchState Back() => Panel is SearchPanelState.Place place
        ? this with
        {
            DisplayText = place.ReturnText,
            CommittedText = place.ReturnText,
            Panel = place.ReturnTo,
            IsEditing = false,
            HighlightedIndex = -1
        }
        : this;

    public ExploreSearchState Clear() => this with { DisplayText = "", CommittedText = "", IsEditing = false, HighlightedIndex = -1, Panel = new SearchPanelState.Idle() };
}
