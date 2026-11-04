using GeoSearch.Web.Client.Search;

namespace GeoSearch.Web.Client.Tests.Search;

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
