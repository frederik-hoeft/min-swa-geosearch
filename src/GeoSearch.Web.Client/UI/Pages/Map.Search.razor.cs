using GeoSearch.Web.Client.Search;
using GeoSearch.Web.Client.Services.Search;
using GeoSearch.Web.Shared.GeoJson;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace GeoSearch.Web.Client.UI.Pages;

public partial class Map
{
    private ExploreSearchState _searchState = ExploreSearchState.Initial;
    private SearchFetchResult? _preview;
    private string? _previewQuery;
    private bool _previewLoading;
    private long _draftRevision;
    private long _committedRevision;
    private CancellationTokenSource? _debounce;
    private SearchQuerySession? _searchSession;

    [Inject]
    private SearchApiClient SearchApi { get; set; } = null!;

    private bool ShowSuggestions => _searchState.IsEditing && !string.IsNullOrWhiteSpace(_searchState.DisplayText);

    private IReadOnlyList<SearchSuggestion> Suggestions
    {
        get
        {
            string query = _searchState.DisplayText.Trim();
            if (!ValidQuery(query))
            {
                return [];
            }

            List<SearchSuggestion> suggestions = [new SearchSuggestion.LiteralQuery(query)];
            if (_previewQuery == query && _preview is SearchFetchResult.Success success)
            {
                suggestions.AddRange(success.Collection.Features.Take(3).Select(feature => new SearchSuggestion.Place(feature)));
            }

            return suggestions;
        }
    }

    private string? ActiveSuggestionId => _searchState.HighlightedIndex is >= 0 && _searchState.HighlightedIndex < Suggestions.Count
        ? SuggestionId(_searchState.HighlightedIndex)
        : null;

    private string? SearchInputHint
    {
        get
        {
            string query = _searchState.DisplayText.Trim();
            if (query.Length < SearchQueryPolicy.MinimumLength)
            {
                return $"Enter at least {SearchQueryPolicy.MinimumLength} characters to search.";
            }
            if (query.Length > SearchQueryPolicy.MaximumLength)
            {
                return $"Search text must be at most {SearchQueryPolicy.MaximumLength} characters.";
            }
            if (_previewQuery == query)
            {
                return _preview switch
                {
                    SearchFetchResult.Failure error => error.Message,
                    SearchFetchResult.Success { Collection.Features.Count: 0 } => "No matching places. You can still search the literal text.",
                    _ => null
                };
            }

            return _previewLoading ? "Finding suggestions..." : null;
        }
    }

    private static string SuggestionId(int index) => $"map-search-suggestion-{index}";

    private static bool ValidQuery(string query) => SearchQueryPolicy.IsValid(query);

    private void OnSearchInput(ChangeEventArgs args)
    {
        string text = args.Value?.ToString() ?? "";
        _searchState = _searchState.Edit(text);
        long revision = ++_draftRevision;
        StopDebounce();
        _preview = null;
        _previewQuery = null;
        string query = text.Trim();
        _previewLoading = ValidQuery(query);
        if (_previewLoading)
        {
            CancellationTokenSource debounce = new();
            _debounce = debounce;
            _ = SearchPreviewAfterDebounceAsync(query, revision, debounce.Token);
        }
    }

    private async Task SearchPreviewAfterDebounceAsync(string query, long revision, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(SearchQueryPolicy.DebounceMilliseconds, cancellationToken);
            if (_disposed || cancellationToken.IsCancellationRequested)
            {
                return;
            }

            SearchFetchResult result = await _searchSession!.FetchAsync(query);
            if (_disposed || _draftRevision != revision || !_searchState.IsEditing || _searchState.DisplayText.Trim() != query)
            {
                return;
            }

            _previewQuery = query;
            _preview = result;
            _previewLoading = false;
            await InvokeAsync(StateHasChanged);
        }
        catch (OperationCanceledException) when (_disposed || cancellationToken.IsCancellationRequested)
        {
            // A newer edit or page disposal superseded this preview.
        }
        catch (ObjectDisposedException) when (_disposed)
        {
            // Initialization was superseded by teardown.
        }
    }

    private async Task OnSearchKeyDownAsync(KeyboardEventArgs args)
    {
        switch (args.Key)
        {
            case "ArrowDown" when ShowSuggestions:
                _searchState = _searchState.Highlight(Math.Min(_searchState.HighlightedIndex + 1, Suggestions.Count - 1));
                break;
            case "ArrowUp" when ShowSuggestions:
                _searchState = _searchState.Highlight(Math.Max(-1, _searchState.HighlightedIndex - 1));
                break;
            case "Enter":
                if (ShowSuggestions && _searchState.HighlightedIndex is >= 0 && _searchState.HighlightedIndex < Suggestions.Count)
                {
                    await ChooseSuggestionAsync(Suggestions[_searchState.HighlightedIndex]);
                }
                else if (ShowSuggestions)
                {
                    await CommitLiteralAsync(_searchState.DisplayText.Trim());
                }
                break;
            case "Escape":
                if (_searchState.IsEditing)
                {
                    DismissDraft();
                }
                else if (_searchState.Panel is SearchPanelState.Place)
                {
                    BackToPrevious();
                }
                break;
        }
    }

    private Task ChooseSuggestionAsync(SearchSuggestion suggestion) => suggestion switch
    {
        SearchSuggestion.LiteralQuery query => CommitLiteralAsync(query.Query),
        SearchSuggestion.Place place => SelectPlaceAsync(place.Feature),
        _ => Task.CompletedTask // Nearby actions are not surfaced until F5 has an editor.
    };

    private async Task CommitLiteralAsync(string query)
    {
        if (!ValidQuery(query))
        {
            return;
        }

        ++_draftRevision;
        StopDebounce();
        _previewLoading = false;
        long revision = ++_committedRevision;
        _searchState = _searchState.CommitQuery(query);
        if (_previewQuery == query && _preview is SearchFetchResult.Success cached)
        {
            _searchState = _searchState.WithForwardResult(query, new SearchLoadState.Ready(cached.Collection));
            return;
        }

        await LoadCommittedAsync(query, 10, revision, refresh: false);
    }

    private async Task LoadCommittedAsync(string query, int limit, long revision, bool refresh)
    {
        try
        {
            Task<SearchFetchResult> request = refresh
                ? _searchSession!.RefreshAsync(query, limit)
                : _searchSession!.FetchAsync(query, limit);
            SearchFetchResult result = await request;
            if (_disposed || _committedRevision != revision)
            {
                return;
            }

            SearchLoadState status = result switch
            {
                SearchFetchResult.Success success => new SearchLoadState.Ready(success.Collection),
                SearchFetchResult.Failure failure => new SearchLoadState.Failed(failure.Message),
                _ => throw new InvalidOperationException("Unexpected search outcome.")
            };
            _searchState = _searchState.WithForwardResult(query, status);
            await InvokeAsync(StateHasChanged);
        }
        catch (OperationCanceledException) when (_disposed)
        {
            // The page is gone; no result can update it.
        }
    }

    private Task SelectPlaceAsync(GeoJsonFeature feature)
    {
        SelectPlace(feature);
        return Task.CompletedTask;
    }

    private void SelectPlace(GeoJsonFeature feature)
    {
        ++_draftRevision;
        ++_committedRevision;
        StopDebounce();
        _previewLoading = false;
        _searchState = _searchState.CommitPlace(feature);
    }

    private void DismissDraft()
    {
        if (!_searchState.IsEditing)
        {
            return;
        }

        ++_draftRevision;
        StopDebounce();
        _previewLoading = false;
        _searchState = _searchState.AbandonDraft();
    }

    private void BackToPrevious()
    {
        ++_committedRevision;
        _searchState = _searchState.Back();
    }

    private void ClearSearch()
    {
        ++_draftRevision;
        ++_committedRevision;
        StopDebounce();
        _preview = null;
        _previewQuery = null;
        _previewLoading = false;
        _searchState = _searchState.Clear();
    }

    private Task RetrySearchAsync() => _searchState.Panel is SearchPanelState.Forward forward
        ? RestartCommittedAsync(forward.Query, 10)
        : Task.CompletedTask;

    private Task ShowMoreAsync() => _searchState.Panel is SearchPanelState.Forward forward
        ? RestartCommittedAsync(forward.Query, 20)
        : Task.CompletedTask;

    private async Task RestartCommittedAsync(string query, int limit)
    {
        long revision = ++_committedRevision;
        _searchState = _searchState.RequestMore(query, limit);
        await LoadCommittedAsync(query, limit, revision, refresh: true);
    }

    private void StopDebounce()
    {
        CancellationTokenSource? debounce = _debounce;
        _debounce = null;
        if (debounce is not null)
        {
            debounce.Cancel();
            debounce.Dispose();
        }
    }
}
