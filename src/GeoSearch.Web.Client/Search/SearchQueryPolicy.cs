namespace GeoSearch.Web.Client.Search;

/// <summary>Frontend policy for autocomplete; the API accepts shorter queries independently.</summary>
internal static class SearchQueryPolicy
{
    public const int MINIMUM_LENGTH = 3;
    public const int MAXIMUM_LENGTH = 200;
    public const int DEBOUNCE_MILLISECONDS = 350;

    public static bool IsValid(string trimmedText) =>
        trimmedText.Length is >= MINIMUM_LENGTH and <= MAXIMUM_LENGTH;
}
