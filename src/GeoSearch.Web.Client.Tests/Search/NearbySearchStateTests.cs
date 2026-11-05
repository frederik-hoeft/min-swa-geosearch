using GeoSearch.Web.Client.Search;
using GeoSearch.Web.Client.Search.Model;
using GeoSearch.Web.Shared.GeoJson;

namespace GeoSearch.Web.Client.Tests.Search;

[TestClass]
public sealed class NearbySearchStateTests
{
    private static readonly GeoCoordinate s_origin = new(52.376, 9.732);

    [TestMethod]
    public void OpeningCategoryOnlyPreparesDraftAndBackRestoresPreviousSearch()
    {
        ExploreSearchState prior = ExploreSearchState.Initial.CommitQuery("hannover")
            .WithForwardResult("hannover", new ReadySearch(ExploreSearchStateTests.Collection("old place")));
        ExploreSearchState editor = prior.OpenNearbyEditor(new NearbyCriteria("Apotheke"));
        NearbySearchPanel nearby = (NearbySearchPanel)editor.Panel;

        Assert.IsTrue(nearby.Editing);
        Assert.IsNull(nearby.Applied);
        Assert.IsNull(nearby.Results);
        Assert.AreEqual("Apotheke", editor.DisplayText);
        Assert.AreSame(prior.Panel, editor.Back().Panel);
        Assert.AreEqual("hannover", editor.Back().DisplayText);
    }

    [TestMethod]
    public void EditingCriteriaKeepsAppliedResultsAndOriginUntilNewSubmit()
    {
        NearbyCriteria criteria = new("Apotheke", 500);
        AppliedNearbyQuery applied = new(criteria, s_origin);
        GeoJsonFeatureCollection collection = ExploreSearchStateTests.Collection("pharmacy");
        ExploreSearchState committed = ExploreSearchState.Initial.OpenNearbyEditor(criteria).CommitNearby(applied)
            .WithNearbyResult(applied, new ReadySearch(collection));

        ExploreSearchState editing = committed.StartEditingNearby()
            .EditNearby(criteria with { Category = "Supermarkt", RadiusMeters = 2000 });
        NearbySearchPanel panel = (NearbySearchPanel)editing.Panel;
        Assert.AreEqual(applied, panel.Applied);
        Assert.IsInstanceOfType<ReadySearch>(panel.Results);
        Assert.AreEqual("Supermarkt", panel.Draft.Category);
        SearchMapState markers = SearchMapState.FromPanel(panel);
        Assert.AreEqual(applied, markers.Nearby);
        Assert.AreEqual("pharmacy", markers.Results[0].Properties.Label);

        ExploreSearchState canceled = editing.CancelNearbyEdit();
        Assert.IsFalse(((NearbySearchPanel)canceled.Panel).Editing);
        Assert.AreEqual(criteria, ((NearbySearchPanel)canceled.Panel).Draft);

        AppliedNearbyQuery next = new(panel.Draft, new GeoCoordinate(52.38, 9.74));
        ExploreSearchState submitted = editing.CommitNearby(next);
        Assert.IsInstanceOfType<LoadingSearch>(((NearbySearchPanel)submitted.Panel).Results);
        Assert.AreEqual(next, SearchMapState.FromPanel(submitted.Panel).Nearby);
        Assert.AreEqual(0, SearchMapState.FromPanel(submitted.Panel).Results.Count);
        Assert.AreSame(submitted, submitted.WithNearbyResult(applied, new FailedSearch("stale")));
    }

    [TestMethod]
    public void InspectionAndPlaceSelectionPreserveCapturedNearbySnapshot()
    {
        NearbyCriteria criteria = new(null, 1000, NearbyOriginKind.SelectedPoint, s_origin);
        AppliedNearbyQuery applied = new(criteria, s_origin);
        ExploreSearchState nearby = ExploreSearchState.Initial.OpenNearbyEditor(criteria)
            .CommitNearby(applied).WithNearbyResult(applied,
                new ReadySearch(ExploreSearchStateTests.Collection("nearby result")));
        ExploreSearchState reverse = nearby.InspectMapPoint(new GeoCoordinate(52.4, 9.8));
        Assert.AreEqual(applied, SearchMapState.FromPanel(reverse.Panel).Nearby);
        Assert.AreSame(nearby.Panel, reverse.Back().Panel);

        GeoJsonFeature feature = ((ReadySearch)((NearbySearchPanel)nearby.Panel).Results!).Collection.Features[0];
        ExploreSearchState details = nearby.CommitPlace(feature);
        Assert.AreEqual(applied, SearchMapState.FromPanel(details.Panel).Nearby);
        Assert.AreSame(nearby.Panel, details.Back().Panel);
    }

    [TestMethod]
    public void NewNearbyEditorFromReverseKeepsRealReturnTargetAndSelectedPoint()
    {
        ExploreSearchState search = ExploreSearchState.Initial.CommitQuery("query");
        ExploreSearchState inspected = search.InspectMapPoint(s_origin);
        ExploreSearchState editor = inspected.OpenNearbyEditor(new NearbyCriteria(
            OriginKind: NearbyOriginKind.SelectedPoint, SelectedPoint: s_origin));
        NearbySearchPanel nearby = (NearbySearchPanel)editor.Panel;
        Assert.AreEqual(s_origin, nearby.Draft.SelectedPoint);
        Assert.AreEqual(s_origin, SearchMapState.FromPanel(editor.Panel).InspectionPoint);
        Assert.AreSame(search.Panel, editor.Back().Panel);
    }

    [TestMethod]
    public void BackFromAppliedNearbyRestoresPreviousCommittedSearch()
    {
        ExploreSearchState prior = ExploreSearchState.Initial.CommitQuery("Hannover")
            .WithForwardResult("Hannover", new ReadySearch(ExploreSearchStateTests.Collection("original")));
        NearbyCriteria criteria = new("Apotheke", 500);
        AppliedNearbyQuery applied = new(criteria, s_origin);
        ExploreSearchState nearby = prior.OpenNearbyEditor(criteria)
            .CommitNearby(applied)
            .WithNearbyResult(applied, new ReadySearch(ExploreSearchStateTests.Collection("nearby")));

        ExploreSearchState restored = nearby.Back();
        Assert.AreSame(prior.Panel, restored.Panel);
        Assert.AreEqual(prior.CommittedText, restored.DisplayText);
        Assert.AreEqual("original", SearchMapState.FromPanel(restored.Panel).Results[0].Properties.Label);
    }

    [TestMethod]
    public void InvalidRadiusOrSelectedPointCannotBeApplied()
    {
        Assert.IsFalse(new NearbyCriteria(RadiusMeters: 300).IsValid);
        Assert.IsFalse(new NearbyCriteria(OriginKind: NearbyOriginKind.SelectedPoint).IsValid);
        Assert.IsFalse(new NearbyCriteria(Category: " ").IsValid);
    }
}
