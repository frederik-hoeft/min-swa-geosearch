using System.Collections.ObjectModel;

namespace GeoSearch.Web.Client.Services.Testing;

/// <summary>A fixed Lab 2 free-text query and the outcome specified by the instructor.</summary>
public sealed record SearchFixture(string Query, string Expected, bool KnownRankingIssue = false);

/// <summary>Source-of-truth set of ten instructor-specified search fixtures.</summary>
public static class SearchFixtureCatalog
{
    public static IReadOnlyList<SearchFixture> All { get; } = new ReadOnlyCollection<SearchFixture>(
    [
        new("Limmerstraße 12", "Address: Limmerstraße 12, Linden-Nord"),
        new("Limmerstr 12 Hannover", "Address: Limmerstraße 12, Linden-Nord"),
        new("Rathuas Linden", "Lindener Rathaus (typo tolerance)"),
        new("Linden-Mitte", "Place"),
        new("Garbsen", "Municipality / place"),
        new("Herrenhäuser Gärten", "POI"),
        new("Lister Meile", "Street"),
        new("Hauptstrasse Laatzen", "Known incorrect result; investigate", true),
        new("Zoo Hannover", "Known incorrect result; investigate", true),
        new("Markthalle Hannover", "Known incorrect result; investigate", true),
    ]);
}
