using GeoSearch.Web.Client.Search.Model;
using GeoSearch.Web.Shared.GeoJson;

namespace GeoSearch.Web.Client.Search;

/// <summary>Immutable presentation state. Draft edits never mutate the committed panel.</summary>
internal sealed record ExploreSearchState(
    string DisplayText,
    string CommittedText,
    bool IsEditing,
    int HighlightedIndex,
    SearchPanelState Panel)
{
    public static ExploreSearchState Initial { get; } = new("", "", false, -1, new IdleSearchPanel());

    public ExploreSearchState Edit(string text) =>
        this with { DisplayText = text, IsEditing = true, HighlightedIndex = -1 };

    public ExploreSearchState Highlight(int index) => this with { HighlightedIndex = index };

    public ExploreSearchState AbandonDraft() =>
        this with { DisplayText = CommittedText, IsEditing = false, HighlightedIndex = -1 };

    public ExploreSearchState CommitQuery(string query) =>
        this with { DisplayText = query, CommittedText = query, IsEditing = false, HighlightedIndex = -1, Panel = new ForwardSearchPanel(query, new LoadingSearch()) };

    public ExploreSearchState CommitPlace(GeoJsonFeature feature) =>
        new(feature.Properties.Label, feature.Properties.Label, false, -1,
            new PlaceSearchPanel(feature, Panel, CommittedText));

    public ExploreSearchState RequestMore(string query, int limit) =>
        Panel is ForwardSearchPanel current && current.Query == query
            ? this with { Panel = current with { Limit = limit, Results = new LoadingSearch() } }
            : this;

    public ExploreSearchState WithForwardResult(string query, SearchLoadState result) =>
        Panel is ForwardSearchPanel current && current.Query == query
            ? this with { Panel = current with { Results = result } }
            : this;

    public ExploreSearchState Back() => Panel is PlaceSearchPanel place
        ? this with
        {
            DisplayText = place.ReturnText,
            CommittedText = place.ReturnText,
            Panel = place.ReturnTo,
            IsEditing = false,
            HighlightedIndex = -1
        }
        : this;

    public ExploreSearchState Clear() => this with { DisplayText = "", CommittedText = "", IsEditing = false, HighlightedIndex = -1, Panel = new IdleSearchPanel() };
}
