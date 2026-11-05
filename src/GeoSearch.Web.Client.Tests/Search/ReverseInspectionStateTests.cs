using GeoSearch.Web.Client.Search;
using GeoSearch.Web.Client.Search.Model;
using GeoSearch.Web.Shared.GeoJson;

namespace GeoSearch.Web.Client.Tests.Search;

[TestClass]
public sealed class ReverseInspectionStateTests
{
    private static readonly GeoCoordinate s_firstPoint = new(52.3759, 9.732);
    private static readonly GeoCoordinate s_secondPoint = new(52.371, 9.729);

    [TestMethod]
    public void MapInspectionPreservesCommittedListAndRestoresDraftOnBack()
    {
        GeoJsonFeatureCollection results = ExploreSearchStateTests.Collection("first");
        ExploreSearchState committed = ExploreSearchState.Initial.CommitQuery("hannover")
            .WithForwardResult("hannover", new ReadySearch(results));

        ExploreSearchState inspection = committed.Edit("uncommitted").InspectMapPoint(s_firstPoint);
        Assert.IsFalse(inspection.IsEditing);
        Assert.AreEqual("hannover", inspection.DisplayText);
        Assert.IsInstanceOfType<ReverseInspectionPanel>(inspection.Panel);
        Assert.AreSame(committed.Panel, ((ReverseInspectionPanel)inspection.Panel).ReturnTo);
        Assert.AreEqual(s_firstPoint, SearchMapState.FromPanel(inspection.Panel).InspectionPoint);
        Assert.AreEqual(1, SearchMapState.FromPanel(inspection.Panel).Results.Count);

        ExploreSearchState restored = inspection.Back();
        Assert.AreSame(committed.Panel, restored.Panel);
        Assert.AreEqual("hannover", restored.DisplayText);
        Assert.IsNull(SearchMapState.FromPanel(restored.Panel).InspectionPoint);
    }

    [TestMethod]
    public void RepeatedClicksReplaceInspectionWithoutStackingAndIgnoreOldResults()
    {
        ExploreSearchState initial = ExploreSearchState.Initial.CommitQuery("hannover");
        ExploreSearchState first = initial.InspectMapPoint(s_firstPoint);
        ExploreSearchState second = first.InspectMapPoint(s_secondPoint);

        Assert.IsInstanceOfType<ReverseInspectionPanel>(second.Panel);
        Assert.AreSame(initial.Panel, ((ReverseInspectionPanel)second.Panel).ReturnTo);
        Assert.AreSame(second, second.WithReverseResult(s_firstPoint, new FailedSearch("outdated")));
        Assert.AreEqual(s_secondPoint, SearchMapState.FromPanel(second.Panel).InspectionPoint);
        Assert.AreSame(initial.Panel, second.Back().Panel);
    }

    [TestMethod]
    public void InspectionResultsUseClickedCoordinateNotReturnedFeatureGeometry()
    {
        GeoJsonFeatureCollection nearest = ExploreSearchStateTests.Collection("other feature");
        ExploreSearchState inspection = ExploreSearchState.Initial.InspectMapPoint(s_firstPoint)
            .WithReverseResult(s_firstPoint, new ReadySearch(nearest));

        ReverseInspectionPanel panel = (ReverseInspectionPanel)inspection.Panel;
        Assert.AreEqual(s_firstPoint, panel.Point);
        Assert.AreEqual("other feature", ((ReadySearch)panel.Result).Collection.Features[0].Properties.Label);
        Assert.AreEqual(s_firstPoint, SearchMapState.FromPanel(panel).InspectionPoint);
        Assert.AreEqual(0, SearchMapState.FromPanel(panel).Results.Count);
    }

    [TestMethod]
    public void PendingForwardCompletionUpdatesReturnTargetUnderInspection()
    {
        ExploreSearchState inspecting = ExploreSearchState.Initial.CommitQuery("hannover")
            .InspectMapPoint(s_firstPoint);
        GeoJsonFeatureCollection results = ExploreSearchStateTests.Collection("first");

        ExploreSearchState completed = inspecting.WithForwardResult("hannover", new ReadySearch(results));
        Assert.IsInstanceOfType<ReverseInspectionPanel>(completed.Panel);
        Assert.AreEqual(1, SearchMapState.FromPanel(completed.Panel).Results.Count);
        Assert.AreEqual(1, SearchMapState.FromPanel(completed.Back().Panel).Results.Count);
    }

    [TestMethod]
    public void SelectingKnownPlaceFromInspectionReturnsToRealDiscovery()
    {
        GeoJsonFeatureCollection results = ExploreSearchStateTests.Collection("first");
        ExploreSearchState baseState = ExploreSearchState.Initial.CommitQuery("hannover")
            .WithForwardResult("hannover", new ReadySearch(results));
        ExploreSearchState inspection = baseState.InspectMapPoint(s_firstPoint);
        ExploreSearchState place = inspection.CommitPlace(results.Features[0]);

        Assert.IsInstanceOfType<PlaceSearchPanel>(place.Panel);
        Assert.AreSame(baseState.Panel, ((PlaceSearchPanel)place.Panel).ReturnTo);
        Assert.IsNull(SearchMapState.FromPanel(place.Panel).InspectionPoint);
        Assert.AreSame(baseState.Panel, place.Back().Panel);
    }

    [TestMethod]
    public void InvalidCoordinatesDoNotEnterInspectionState()
    {
        Assert.IsFalse(new GeoCoordinate(double.NaN, 9.7).IsValid);
        Assert.IsFalse(new GeoCoordinate(91, 9.7).IsValid);
        Assert.IsFalse(new GeoCoordinate(52.3, -181).IsValid);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => ExploreSearchState.Initial.InspectMapPoint(new GeoCoordinate(91, 9)));
    }
}
