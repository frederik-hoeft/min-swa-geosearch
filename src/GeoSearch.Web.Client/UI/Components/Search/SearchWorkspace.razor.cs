using GeoSearch.Web.Client.Search;
using GeoSearch.Web.Client.Search.Model;
using GeoSearch.Web.Client.Services.Search;
using GeoSearch.Web.Shared.GeoJson;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace GeoSearch.Web.Client.UI.Components.Search;

public sealed partial class SearchWorkspace : ComponentBase, IDisposable
{
    private bool _disposed;

    [Parameter]
    public EventCallback OnCommitted { get; set; }

    [Parameter]
    public EventCallback<SearchMapState> OnMapStateChanged { get; set; }

    [Parameter]
    public EventCallback OnToggle { get; set; }

    [Parameter]
    public bool IsEffectivelyExpanded { get; set; }

    protected override void OnInitialized() => _searchSession = new SearchQuerySession(SearchApi);

    public void Dispose()
    {
        _disposed = true;
        StopDebounce();
        _searchSession?.Dispose();
        GC.SuppressFinalize(this);
    }

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

            List<SearchSuggestion> suggestions = [new LiteralQuerySuggestion(query)];
            if (_previewQuery == query && _preview is SearchSuccess success)
            {
                suggestions.AddRange(success.Collection.Features.Take(3).Select(feature => new PlaceSuggestion(feature)));
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
            if (query.Length < SearchQueryPolicy.MINIMUM_LENGTH)
            {
                return $"Enter at least {SearchQueryPolicy.MINIMUM_LENGTH} characters to search.";
            }
            if (query.Length > SearchQueryPolicy.MAXIMUM_LENGTH)
            {
                return $"Search text must be at most {SearchQueryPolicy.MAXIMUM_LENGTH} characters.";
            }
            if (_previewQuery == query)
            {
                return _preview switch
                {
                    SearchFailure error => error.Message,
                    SearchSuccess { Collection.Features.Count: 0 } => "No matching places. You can still search the literal text.",
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
            await Task.Delay(SearchQueryPolicy.DEBOUNCE_MILLISECONDS, cancellationToken);
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
                    AbandonDraft();
                }
                else if (_searchState.Panel is PlaceSearchPanel)
                {
                    await BackToPreviousAsync();
                }
                break;
        }
    }

    private Task ChooseSuggestionAsync(SearchSuggestion suggestion) => suggestion switch
    {
        LiteralQuerySuggestion query => CommitLiteralAsync(query.Query),
        PlaceSuggestion place => SelectAutocompletePlaceAsync(place.Feature),
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
        await OnCommitted.InvokeAsync();
        await NotifyMapStateChangedAsync();
        if (_previewQuery == query && _preview is SearchSuccess cached)
        {
            _searchState = _searchState.WithForwardResult(query, new ReadySearch(cached.Collection));
            await NotifyMapStateChangedAsync();
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
                SearchSuccess success => new ReadySearch(success.Collection),
                SearchFailure failure => new FailedSearch(failure.Message),
                _ => throw new InvalidOperationException("Unexpected search outcome.")
            };
            _searchState = _searchState.WithForwardResult(query, status);
            await NotifyMapStateChangedAsync();
            await InvokeAsync(StateHasChanged);
        }
        catch (OperationCanceledException) when (_disposed)
        {
            // The page is gone; no result can update it.
        }
    }

    private Task SelectAutocompletePlaceAsync(GeoJsonFeature feature) => SelectPlaceCoreAsync(feature, showReturnResults: false);

    private Task SelectPlaceAsync(GeoJsonFeature feature) => SelectPlaceCoreAsync(feature, showReturnResults: true);

    private async Task SelectPlaceCoreAsync(GeoJsonFeature feature, bool showReturnResults)
    {
        ++_draftRevision;
        ++_committedRevision;
        StopDebounce();
        _previewLoading = false;
        _searchState = _searchState.CommitPlace(feature, showReturnResults);
        await OnCommitted.InvokeAsync();
        await NotifyMapStateChangedAsync();
    }

    public Task DismissDraftAsync()
    {
        if (_disposed || !_searchState.IsEditing)
        {
            return Task.CompletedTask;
        }

        AbandonDraft();
        return InvokeAsync(StateHasChanged);
    }

    private void AbandonDraft()
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

    private async Task BackToPreviousAsync()
    {
        ++_committedRevision;
        _searchState = _searchState.Back();
        await NotifyMapStateChangedAsync();
    }

    private async Task ClearSearchAsync()
    {
        ++_draftRevision;
        ++_committedRevision;
        StopDebounce();
        _preview = null;
        _previewQuery = null;
        _previewLoading = false;
        _searchState = _searchState.Clear();
        await NotifyMapStateChangedAsync();
    }

    private Task RetrySearchAsync() => _searchState.Panel is ForwardSearchPanel forward
        ? RestartCommittedAsync(forward.Query, 10)
        : Task.CompletedTask;

    private Task ShowMoreAsync() => _searchState.Panel is ForwardSearchPanel forward
        ? RestartCommittedAsync(forward.Query, 20)
        : Task.CompletedTask;

    private async Task RestartCommittedAsync(string query, int limit)
    {
        long revision = ++_committedRevision;
        _searchState = _searchState.RequestMore(query, limit);
        await NotifyMapStateChangedAsync();
        await LoadCommittedAsync(query, limit, revision, refresh: true);
    }

    // Map marker selection reuses the known feature; no new HTTP or reverse request.
    public async Task SelectMapFeatureAsync(GeoJsonFeature feature)
    {
        if (_disposed)
        {
            return;
        }

        SearchMapState map = SearchMapState.FromPanel(_searchState.Panel);
        string id = feature.Properties.Id;
        if (!map.Results.Any(result => result.Properties.Id == id) && map.SelectedFeature?.Properties.Id != id)
        {
            return;
        }

        await SelectPlaceAsync(feature);
        // This method is invoked through @ref by Map, not by a SearchWorkspace UI
        // event. Parent callbacks update the map, but do not guarantee that Blazor
        // renders this child's updated details state when its parameters are unchanged.
        if (!_disposed)
        {
            await InvokeAsync(StateHasChanged);
        }
    }

    private Task NotifyMapStateChangedAsync() => OnMapStateChanged.InvokeAsync(SearchMapState.FromPanel(_searchState.Panel));

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
