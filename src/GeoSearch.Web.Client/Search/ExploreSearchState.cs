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

    public ExploreSearchState CommitPlace(GeoJsonFeature feature, bool showReturnResults = true)
    {
        // A new inspection replaces the foreground one. Back always targets the
        // real committed results (or idle), never a chain of prior inspections.
        if (Panel is PlaceSearchPanel current && current.Feature.Properties.Id == feature.Properties.Id &&
            current.ShowReturnResults == showReturnResults)
        {
            // A marker can be clicked while an autocomplete draft is open.
            // Even a re-selection must abandon that draft and show committed details.
            return this with
            {
                DisplayText = current.Feature.Properties.Label,
                CommittedText = current.Feature.Properties.Label,
                IsEditing = false,
                HighlightedIndex = -1
            };
        }

        SearchPanelState underlying = Panel is ReverseInspectionPanel reverse ? reverse.ReturnTo : Panel;
        SearchPanelState returnTo = underlying is PlaceSearchPanel previous ? previous.ReturnTo : underlying;
        string returnText = underlying is PlaceSearchPanel place ? place.ReturnText
            : Panel is ReverseInspectionPanel inspection ? inspection.ReturnText : CommittedText;
        return new(feature.Properties.Label, feature.Properties.Label, false, -1,
            new PlaceSearchPanel(feature, returnTo, returnText, showReturnResults));
    }

    /// <summary>Open an editable nearby intent without issuing a request.</summary>
    public ExploreSearchState OpenNearbyEditor(NearbyCriteria criteria)
    {
        if (!criteria.IsValid)
        {
            throw new ArgumentOutOfRangeException(nameof(criteria));
        }

        SearchPanelState previous = Panel is ReverseInspectionPanel inspection ? inspection.ReturnTo : Panel;
        SearchPanelState returnTo = previous is PlaceSearchPanel place ? place.ReturnTo : previous;
        string returnText = previous is PlaceSearchPanel selected ? selected.ReturnText : CommittedText;
        string label = criteria.Category ?? "Explore nearby";
        return this with
        {
            DisplayText = label,
            CommittedText = label,
            IsEditing = false,
            HighlightedIndex = -1,
            Panel = new NearbySearchPanel(criteria, null, null, true, returnTo, returnText)
        };
    }

    public ExploreSearchState EditNearby(NearbyCriteria criteria) => Panel is NearbySearchPanel nearby && criteria.IsValid
        ? this with { Panel = nearby with { Draft = criteria, Editing = true } }
        : this;

    public ExploreSearchState StartEditingNearby() => Panel is NearbySearchPanel nearby
        ? this with { Panel = nearby with { Draft = nearby.Applied?.Criteria ?? nearby.Draft, Editing = true } }
        : this;

    public ExploreSearchState CancelNearbyEdit() => Panel is NearbySearchPanel nearby
        ? nearby.Applied is not null
            ? this with { Panel = nearby with { Draft = nearby.Applied.Criteria, Editing = false } }
            : Back()
        : this;

    public ExploreSearchState CommitNearby(AppliedNearbyQuery applied)
    {
        if (!applied.IsValid || Panel is not NearbySearchPanel nearby)
        {
            throw new InvalidOperationException("A valid nearby draft must be active before submitting.");
        }

        return this with
        {
            DisplayText = applied.Criteria.Category ?? "Explore nearby",
            CommittedText = applied.Criteria.Category ?? "Explore nearby",
            IsEditing = false,
            HighlightedIndex = -1,
            Panel = nearby with { Draft = applied.Criteria, Applied = applied, Results = new LoadingSearch(), Editing = false }
        };
    }

    public ExploreSearchState WithNearbyResult(AppliedNearbyQuery applied, SearchLoadState result) => Panel switch
    {
        NearbySearchPanel { Applied: { } current } nearby when current == applied =>
            this with { Panel = nearby with { Results = result } },
        ReverseInspectionPanel { ReturnTo: NearbySearchPanel { Applied: { } current } nearby } inspection when current == applied =>
            this with { Panel = inspection with { ReturnTo = nearby with { Results = result } } },
        _ => this
    };

    /// <summary>Inspect a background-map coordinate without changing committed discovery.</summary>
    public ExploreSearchState InspectMapPoint(GeoCoordinate point)
    {
        if (!point.IsValid)
        {
            throw new ArgumentOutOfRangeException(nameof(point));
        }
        SearchPanelState returnTo = Panel is ReverseInspectionPanel prior ? prior.ReturnTo : Panel;
        string returnText = Panel is ReverseInspectionPanel previous ? previous.ReturnText : CommittedText;
        return this with
        {
            DisplayText = returnText,
            CommittedText = returnText,
            IsEditing = false,
            HighlightedIndex = -1,
            Panel = new ReverseInspectionPanel(point, new LoadingSearch(), returnTo, returnText)
        };
    }

    public ExploreSearchState WithReverseResult(GeoCoordinate point, SearchLoadState result) =>
        Panel is ReverseInspectionPanel current && current.Point == point
            ? this with { Panel = current with { Result = result } }
            : this;

    public ExploreSearchState RetryReverse() =>
        Panel is ReverseInspectionPanel current
            ? this with { Panel = current with { Result = new LoadingSearch() } }
            : this;

    public ExploreSearchState RequestMore(string query, int limit) =>
        Panel is ForwardSearchPanel current && current.Query == query
            ? this with { Panel = current with { Limit = limit, Results = new LoadingSearch() } }
            : this;

    public ExploreSearchState WithForwardResult(string query, SearchLoadState result) => Panel switch
    {
        ForwardSearchPanel current when current.Query == query => this with
        {
            Panel = current with { Results = result }
        },
        ReverseInspectionPanel { ReturnTo: ForwardSearchPanel previous } inspection when previous.Query == query => this with
        {
            Panel = inspection with { ReturnTo = previous with { Results = result } }
        },
        _ => this
    };

    public ExploreSearchState Back() => Panel is NearbySearchPanel nearby
        ? this with
        {
            DisplayText = nearby.ReturnText,
            CommittedText = nearby.ReturnText,
            Panel = nearby.ReturnTo,
            IsEditing = false,
            HighlightedIndex = -1
        }
        : Panel is ReverseInspectionPanel inspection
        ? this with
        {
            DisplayText = inspection.ReturnText,
            CommittedText = inspection.ReturnText,
            Panel = inspection.ReturnTo,
            IsEditing = false,
            HighlightedIndex = -1
        }
        : Panel is PlaceSearchPanel place
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
