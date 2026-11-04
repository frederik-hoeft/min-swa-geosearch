namespace GeoSearch.Web.Client.Search;

/// <summary>Frontend policy for autocomplete; the API accepts shorter queries independently.</summary>
internal static class SearchQueryPolicy
{
    public const int MinimumLength = 3;
    public const int MaximumLength = 200;
    public const int DebounceMilliseconds = 350;

    public static bool IsValid(string trimmedText) =>
        trimmedText.Length is >= MinimumLength and <= MaximumLength;
}
