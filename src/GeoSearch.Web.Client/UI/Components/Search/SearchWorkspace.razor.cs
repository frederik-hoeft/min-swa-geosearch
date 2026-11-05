using GeoSearch.Web.Client.Search;
using GeoSearch.Web.Client.Search.Model;
using GeoSearch.Web.Client.Services.Search;
using GeoSearch.Web.Shared.GeoJson;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace GeoSearch.Web.Client.UI.Components.Search;

/// <summary>Translates UI events into workspace commands and renders their state.</summary>
public sealed partial class SearchWorkspace : ComponentBase, IDisposable
{
    private SearchWorkspaceWorkflow _workflow = null!;
    private bool _disposed;

    [Parameter]
    public EventCallback OnCommitted { get; set; }

    [Parameter]
    public EventCallback<SearchMapState> OnMapStateChanged { get; set; }

    [Parameter]
    public EventCallback OnToggle { get; set; }

    [Parameter]
    public bool IsEffectivelyExpanded { get; set; }

    [Inject]
    private SearchApiClient SearchApi { get; set; } = null!;

    protected override void OnInitialized() => _workflow = new SearchWorkspaceWorkflow(
        SearchApi,
        () => InvokeAsync(StateHasChanged),
        OnCommitted.InvokeAsync,
        state => OnMapStateChanged.InvokeAsync(state),
        InvokeAsync);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _workflow?.Dispose();
        GC.SuppressFinalize(this);
    }

    // Kept internal for renderer integration tests that need to start from an
    // already-committed search without issuing an unrelated HTTP request.
    internal ExploreSearchState SearchState
    {
        get => _workflow.State;
        set => _workflow.State = value;
    }

    private bool ShowSuggestions => _workflow.ShowSuggestions;

    private IReadOnlyList<SearchSuggestion> Suggestions => _workflow.Suggestions;

    private string? SearchInputHint => _workflow.InputHint;

    private string? ActiveSuggestionId => SearchState.HighlightedIndex is >= 0 && SearchState.HighlightedIndex < Suggestions.Count
        ? $"map-search-suggestion-{SearchState.HighlightedIndex}"
        : null;

    private void OnSearchInput(ChangeEventArgs args) => _workflow.Edit(args.Value?.ToString() ?? "");

    private async Task OnSearchKeyDownAsync(KeyboardEventArgs args)
    {
        switch (args.Key)
        {
            case "ArrowDown" when ShowSuggestions:
                _workflow.Highlight(Math.Min(SearchState.HighlightedIndex + 1, Suggestions.Count - 1));
                break;
            case "ArrowUp" when ShowSuggestions:
                _workflow.Highlight(Math.Max(-1, SearchState.HighlightedIndex - 1));
                break;
            case "Enter" when ShowSuggestions:
                if (SearchState.HighlightedIndex is >= 0 && SearchState.HighlightedIndex < Suggestions.Count)
                {
                    await _workflow.ChooseSuggestionAsync(Suggestions[SearchState.HighlightedIndex]);
                }
                else
                {
                    await _workflow.CommitLiteralAsync(SearchState.DisplayText.Trim());
                }
                break;
            case "Escape":
                if (SearchState.IsEditing)
                {
                    _workflow.AbandonDraft();
                }
                else if (SearchState.Panel is PlaceSearchPanel or ReverseInspectionPanel)
                {
                    await _workflow.BackAsync();
                }
                break;
        }
    }

    private Task ChooseSuggestionAsync(SearchSuggestion suggestion) => _workflow.ChooseSuggestionAsync(suggestion);

    private Task SelectPlaceAsync(GeoJsonFeature feature) => _workflow.SelectPlaceAsync(feature);

    private Task BackToPreviousAsync() => _workflow.BackAsync();

    private Task ClearSearchAsync() => _workflow.ClearAsync();

    private Task RetrySearchAsync() => _workflow.RetryForwardAsync();

    private Task ShowMoreAsync() => _workflow.ShowMoreAsync();

    private Task RetryReverseAsync() => _workflow.RetryReverseAsync();

    public async Task DismissDraftAsync()
    {
        if (_disposed || !SearchState.IsEditing)
        {
            return;
        }

        _workflow.AbandonDraft();
        if (!_disposed)
        {
            await InvokeAsync(StateHasChanged);
        }
    }

    public Task HandleMapBackgroundClickAsync(GeoCoordinate point) => _disposed
        ? Task.CompletedTask : _workflow.HandleMapBackgroundClickAsync(point);

    public void CancelPendingMapClick()
    {
        if (!_disposed)
        {
            _workflow.CancelPendingMapClick();
        }
    }

    public Task InspectMapPointAsync(GeoCoordinate point) => _disposed
        ? Task.CompletedTask : InvokeAsync(() => _workflow.InspectMapPointAsync(point));

    public async Task DismissMapInspectionAsync()
    {
        if (_disposed)
        {
            return;
        }

        await _workflow.DismissMapInspectionAsync();
        if (!_disposed)
        {
            await InvokeAsync(StateHasChanged);
        }
    }

    public async Task SelectMapFeatureAsync(GeoJsonFeature feature)
    {
        if (_disposed)
        {
            return;
        }

        await _workflow.SelectMapFeatureAsync(feature);
        // Called through @ref, not via this component's event dispatch.
        if (!_disposed)
        {
            await InvokeAsync(StateHasChanged);
        }
    }
}
