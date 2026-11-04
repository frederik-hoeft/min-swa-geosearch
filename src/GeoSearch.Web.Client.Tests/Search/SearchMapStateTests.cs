using GeoSearch.Web.Client.Search;
using GeoSearch.Web.Client.Search.Model;
using GeoSearch.Web.Shared.GeoJson;

namespace GeoSearch.Web.Client.Tests.Search;

[TestClass]
public sealed class SearchMapStateTests
{
    [TestMethod]
    public void DraftChangesCannotReplaceCommittedMapResults()
    {
        GeoJsonFeatureCollection collection = ExploreSearchStateTests.Collection("Hannover");
        ExploreSearchState committed = ExploreSearchState.Initial.CommitQuery("hannover")
            .WithForwardResult("hannover", new ReadySearch(collection));

        SearchMapState map = SearchMapState.FromPanel(committed.Edit("apotheke").Panel);
        Assert.AreSame(collection.Features, map.Results);
        Assert.IsNull(map.SelectedFeature);
    }

    [TestMethod]
    public void ListSelectionRetainsCommittedMarkersUntilBackOrClear()
    {
        GeoJsonFeatureCollection collection = ExploreSearchStateTests.Collection("Hannover");
        ExploreSearchState search = ExploreSearchState.Initial.CommitQuery("hannover")
            .WithForwardResult("hannover", new ReadySearch(collection));
        ExploreSearchState details = search.CommitPlace(collection.Features[0]);

        SearchMapState selected = SearchMapState.FromPanel(details.Panel);
        Assert.AreSame(collection.Features, selected.Results);
        Assert.AreSame(collection.Features[0], selected.SelectedFeature);

        SearchMapState back = SearchMapState.FromPanel(details.Back().Panel);
        Assert.AreSame(collection.Features, back.Results);
        Assert.IsNull(back.SelectedFeature);

        SearchMapState cleared = SearchMapState.FromPanel(details.Clear().Panel);
        Assert.AreEqual(0, cleared.Results.Count);
        Assert.IsNull(cleared.SelectedFeature);
    }

    [TestMethod]
    public void DirectAutocompletePlaceHasSelectionWithoutInventingResults()
    {
        GeoJsonFeature place = ExploreSearchStateTests.Collection("Hannover").Features[0];
        ExploreSearchState direct = ExploreSearchState.Initial.CommitPlace(place);

        SearchMapState map = SearchMapState.FromPanel(direct.Panel);
        Assert.AreEqual(0, map.Results.Count);
        Assert.AreSame(place, map.SelectedFeature);
        Assert.IsInstanceOfType<IdleSearchPanel>(direct.Back().Panel);
    }

    [TestMethod]
    public void SelectingAnotherMarkerReplacesInspectionWithoutStackingBackTargets()
    {
        GeoJsonFeatureCollection collection = new("snapshot", [
            new GeoJsonFeature(new GeoJsonPoint(9.7, 52.3),
                new GeoJsonProperties("osm:1", "first", "first", "poi", "", null, null)),
            new GeoJsonFeature(new GeoJsonPoint(9.8, 52.4),
                new GeoJsonProperties("osm:2", "second", "second", "poi", "", null, null))]);
        ExploreSearchState committed = ExploreSearchState.Initial.CommitQuery("places")
            .WithForwardResult("places", new ReadySearch(collection));
        ExploreSearchState first = committed.CommitPlace(collection.Features[0]);
        ExploreSearchState second = first.CommitPlace(collection.Features[1]);

        ExploreSearchState reselected = first.Edit("uncommitted").CommitPlace(collection.Features[0]);
        Assert.IsFalse(reselected.IsEditing);
        Assert.AreEqual("first", reselected.DisplayText);
        Assert.AreSame(first.Panel, reselected.Panel);
        Assert.AreSame(committed.Panel, second.Back().Panel);
        Assert.AreEqual("places", second.Back().DisplayText);
        Assert.HasCount(2, SearchMapState.FromPanel(second.Panel).Results);
        Assert.AreSame(collection.Features[1], SearchMapState.FromPanel(second.Panel).SelectedFeature);
    }

    [TestMethod]
    public void NewCommitDropsOldMarkersUntilItsOwnSnapshotArrives()
    {
        GeoJsonFeatureCollection collection = ExploreSearchStateTests.Collection("old");
        ExploreSearchState previous = ExploreSearchState.Initial.CommitQuery("old")
            .WithForwardResult("old", new ReadySearch(collection));

        ExploreSearchState next = previous.CommitQuery("new");
        Assert.AreEqual(0, SearchMapState.FromPanel(next.Panel).Results.Count);
        Assert.AreEqual(0, SearchMapState.FromPanel(next.WithForwardResult("old", new ReadySearch(collection)).Panel).Results.Count);
        Assert.AreSame(collection.Features, SearchMapState.FromPanel(next.WithForwardResult("new", new ReadySearch(collection)).Panel).Results);
    }
}
