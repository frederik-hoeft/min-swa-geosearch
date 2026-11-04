using GeoSearch.Web.Client.Search;
using GeoSearch.Web.Shared.GeoJson;

namespace GeoSearch.Web.Client.Tests.Search;

[TestClass]
public sealed class ExploreSearchStateTests
{
    [TestMethod]
    public void EditingAndAbandoningDraftPreservesCommittedResults()
    {
        GeoJsonFeatureCollection collection = Collection("place-a");
        ExploreSearchState committed = ExploreSearchState.Initial.CommitQuery("hannover")
            .WithForwardResult("hannover", new SearchLoadState.Ready(collection));

        ExploreSearchState editing = committed.Edit("apothe");
        Assert.IsTrue(editing.IsEditing);
        Assert.AreSame(committed.Panel, editing.Panel);
        Assert.AreEqual("apothe", editing.DisplayText);

        ExploreSearchState restored = editing.AbandonDraft();
        Assert.AreEqual("hannover", restored.DisplayText);
        Assert.IsFalse(restored.IsEditing);
        Assert.AreSame(committed.Panel, restored.Panel);
    }

    [TestMethod]
    public void KnownPlaceInspectionReturnsToRealPriorPanel()
    {
        GeoJsonFeatureCollection collection = Collection("market");
        ExploreSearchState search = ExploreSearchState.Initial.CommitQuery("hanno")
            .WithForwardResult("hanno", new SearchLoadState.Ready(collection));

        ExploreSearchState details = search.Edit("other").CommitPlace(collection.Features[0]);
        Assert.AreEqual("market", details.DisplayText);
        Assert.IsInstanceOfType<SearchPanelState.Place>(details.Panel);

        ExploreSearchState back = details.Back();
        Assert.AreSame(search.Panel, back.Panel);
        Assert.AreEqual("hanno", back.DisplayText);
    }

    [TestMethod]
    public void StaleCompletionCannotReplaceNewCommittedQueryOrPlace()
    {
        GeoJsonFeatureCollection collection = Collection("first");
        ExploreSearchState newer = ExploreSearchState.Initial.CommitQuery("first").CommitQuery("second");
        Assert.AreSame(newer, newer.WithForwardResult("first", new SearchLoadState.Ready(collection)));
        Assert.AreSame(newer, newer.WithForwardResult("first", new SearchLoadState.Failed("old error")));

        ExploreSearchState place = newer.CommitPlace(collection.Features[0]);
        Assert.AreSame(place, place.WithForwardResult("second", new SearchLoadState.Ready(collection)));
        Assert.IsInstanceOfType<SearchPanelState.Idle>(place.Clear().Panel);
    }

    internal static GeoJsonFeatureCollection Collection(string label) => new("test", [
        new GeoJsonFeature(new GeoJsonPoint(9.7, 52.3),
            new GeoJsonProperties("osm:1", label, label, "place", "", null, null))
    ]);
}

[TestClass]
public sealed class SearchQueryPolicyTests
{
    [TestMethod]
    [DataRow("ab", false)]
    [DataRow("abc", true)]
    [DataRow("apotheke", true)]
    [DataRow("", false)]
    public void RejectsShortQueries(string value, bool expected) =>
        Assert.AreEqual(expected, SearchQueryPolicy.IsValid(value));

    [TestMethod]
    public void RejectsOverlongQueries()
    {
        Assert.IsTrue(SearchQueryPolicy.IsValid(new string('a', 200)));
        Assert.IsFalse(SearchQueryPolicy.IsValid(new string('a', 201)));
    }
}
