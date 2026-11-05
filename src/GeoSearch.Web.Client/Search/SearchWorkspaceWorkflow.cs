using GeoSearch.Web.Client.Search.Model;
using GeoSearch.Web.Client.Services.Search;
using GeoSearch.Web.Shared.GeoJson;

namespace GeoSearch.Web.Client.Search;

/// <summary>
/// Coordinates the immutable panel state with independent preview, committed
/// search, reverse lookup, and map gesture sessions. The Razor component owns
/// event translation and rendering, not request lifecycles or panel transitions.
/// </summary>
internal sealed class SearchWorkspaceWorkflow : IDisposable
{
    private readonly SearchQuerySession _queries;
    private readonly SearchPreviewSession _preview;
    private readonly CommittedSearchSession _forward;
    private readonly ReverseLookupSession _reverse;
    private readonly NearbyDiscoveryWorkflow _nearbyFlow;
    private readonly DeviceLocationSession _deviceLocation;
    public DeviceLocationFix? Device => _deviceLocation.Device;
    public string? LocationError => _deviceLocation.LocationError;
    public bool Locating => _deviceLocation.Locating;
    private long _navigationRevision;
    private readonly BackgroundMapClickCoordinator _backgroundClicks;
    private readonly Func<Task> _render;
    private readonly Func<Task> _committed;
    private readonly Func<SearchMapState, Task> _mapChanged;
    private bool _disposed;

    public SearchWorkspaceWorkflow(SearchApiClient api, Func<Task> render, Func<Task> committed,
        Func<SearchMapState, Task> mapChanged, Func<Func<Task>, Task> dispatch,
        Func<Task<GeoCoordinate?>>? mapCenter = null,
        Func<CancellationToken, Task<DeviceLocationResult>>? locate = null,
        Func<CancellationToken, Task>? backgroundClickDelay = null)
    {
        _render = render;
        _committed = committed;
        _mapChanged = mapChanged;
        _queries = new SearchQuerySession(api);
        _preview = new SearchPreviewSession(_queries);
        _forward = new CommittedSearchSession(_queries);
        _reverse = new ReverseLookupSession(api);
        _nearbyFlow = new NearbyDiscoveryWorkflow(api,
            () => State, value => State = value, render, committed, NotifyMapAsync,
            mapCenter ?? (() => Task.FromResult<GeoCoordinate?>(null)), () => Device,
            BeforeNearbySubmit);
        _deviceLocation = new DeviceLocationSession(
            locate ?? (_ => Task.FromResult(new DeviceLocationResult(null, "Device location is unavailable."))),
            render, NotifyMapAsync, InspectMapPointAsync, () => _navigationRevision);
        // Delayed confirmations must re-enter the Blazor renderer's sync context.
        _backgroundClicks = new BackgroundMapClickCoordinator(point => dispatch(() => InspectMapPointAsync(point)), backgroundClickDelay);
    }

    public ExploreSearchState State { get; internal set; } = ExploreSearchState.Initial;

    public bool ShowSuggestions => State.IsEditing;

    public IReadOnlyList<SearchSuggestion> Suggestions
    {
        get
        {
            string query = State.DisplayText.Trim();
            if (query.Length == 0)
            {
                return [new ExploreNearbySuggestion()];
            }

            List<SearchSuggestion> suggestions = [];
            if (SearchQueryPolicy.IsValid(query))
            {
                suggestions.Add(new LiteralQuerySuggestion(query));
            }
            suggestions.AddRange(_nearbyFlow.MatchCategories(query).Select(category => new CategorySuggestion(category)));
            suggestions.AddRange(_preview.GetSuggestions(query).OfType<PlaceSuggestion>());
            return suggestions;
        }
    }

    public string? InputHint =>
        (State.DisplayText.Length > 0 ? _preview.GetInputHint(State.DisplayText) : null) ?? _nearbyFlow.CategoryError;

    public IReadOnlyList<string> Categories => _nearbyFlow.Categories;
    public string? CategoryError => _nearbyFlow.CategoryError;
    public string? NearbyError => _nearbyFlow.NearbyError;
    public bool CanSearchThisArea => _nearbyFlow.CanSearchThisArea;

    public void OpenSuggestions()
    {
        AdvanceNavigation();
        if (!State.IsEditing)
        {
            State = State.Edit(State.DisplayText);
        }
        _ = _nearbyFlow.EnsureCategoriesAsync();
    }

    public Task RetryCategoriesAsync() => _nearbyFlow.RetryCategoriesAsync();

    public void Edit(string text)
    {
        AdvanceNavigation();
        _backgroundClicks.CancelPending();
        State = State.Edit(text);
        _preview.Edit(text, _render);
        _ = _nearbyFlow.EnsureCategoriesAsync();
    }

    public void Highlight(int index) => State = State.Highlight(index);

    public async Task CommitLiteralAsync(string query)
    {
        if (_disposed || !SearchQueryPolicy.IsValid(query))
        {
            return;
        }

        _backgroundClicks.CancelPending();
        _preview.CancelPending();
        _reverse.Invalidate();
        _nearbyFlow.Invalidate();
        AdvanceNavigation();
        long revision = _forward.Begin();
        State = State.CommitQuery(query);
        await _committed();
        await NotifyMapAsync();
        if (_preview.GetSuccessfulPreview(query) is { } cached)
        {
            State = State.WithForwardResult(query, new ReadySearch(cached.Collection));
            await NotifyMapAsync();
            return;
        }

        await LoadForwardAsync(query, 10, revision, refresh: false);
    }

    public Task ChooseSuggestionAsync(SearchSuggestion suggestion) => suggestion switch
    {
        LiteralQuerySuggestion query => CommitLiteralAsync(query.Query),
        PlaceSuggestion place => SelectPlaceAsync(place.Feature, showReturnResults: false),
        CategorySuggestion category => OpenNearbyAsync(category.Value),
        ExploreNearbySuggestion => OpenNearbyAsync(null),
        _ => Task.CompletedTask
    };

    public async Task SelectPlaceAsync(GeoJsonFeature feature, bool showReturnResults = true)
    {
        if (_disposed)
        {
            return;
        }

        _backgroundClicks.CancelPending();
        _preview.CancelPending();
        _forward.Invalidate();
        _nearbyFlow.Invalidate();
        _reverse.Invalidate();
        AdvanceNavigation();
        State = State.CommitPlace(feature, showReturnResults);
        await _committed();
        await NotifyMapAsync();
    }

    public void AbandonDraft()
    {
        if (!State.IsEditing)
        {
            return;
        }

        AdvanceNavigation();
        _backgroundClicks.CancelPending();
        _preview.CancelPending();
        State = State.AbandonDraft();
    }

    public async Task BackAsync()
    {
        _backgroundClicks.CancelPending();
        if (State.Panel is ReverseInspectionPanel)
        {
            _reverse.Invalidate();
        }
        else
        {
            _forward.Invalidate();
            _nearbyFlow.Invalidate();
        }

        AdvanceNavigation();
        State = State.Back();
        await NotifyMapAsync();
    }

    public async Task ClearAsync()
    {
        _backgroundClicks.CancelPending();
        _preview.Clear();
        _forward.Invalidate();
        _nearbyFlow.Invalidate();
        _reverse.Invalidate();
        AdvanceNavigation();
        State = State.Clear();
        await NotifyMapAsync();
    }

    public Task RetryForwardAsync() => State.Panel is ForwardSearchPanel forward
        ? RestartForwardAsync(forward.Query, 10)
        : Task.CompletedTask;

    public Task ShowMoreAsync() => State.Panel is ForwardSearchPanel forward
        ? RestartForwardAsync(forward.Query, 20)
        : Task.CompletedTask;

    private async Task RestartForwardAsync(string query, int limit)
    {
        _backgroundClicks.CancelPending();
        long revision = _forward.Begin();
        State = State.RequestMore(query, limit);
        await NotifyMapAsync();
        await LoadForwardAsync(query, limit, revision, refresh: true);
    }

    private async Task LoadForwardAsync(string query, int limit, long revision, bool refresh)
    {
        try
        {
            SearchLoadState? status = await _forward.FetchAsync(query, limit, revision, refresh);
            if (_disposed || status is null)
            {
                return;
            }

            State = State.WithForwardResult(query, status);
            await NotifyMapAsync();
            await _render();
        }
        catch (OperationCanceledException) when (_disposed)
        {
            // The page is gone; no result can update it.
        }
    }

    /// <summary>A map click dismisses a draft; otherwise it waits for double-click disambiguation.</summary>
    public Task HandleMapBackgroundClickAsync(GeoCoordinate point)
    {
        if (_disposed || !point.IsValid)
        {
            return Task.CompletedTask;
        }

        if (State.IsEditing)
        {
            AbandonDraft();
            return _render();
        }

        return _backgroundClicks.ScheduleAsync(point);
    }

    public void CancelPendingMapClick() => _backgroundClicks.CancelPending();

    public async Task InspectMapPointAsync(GeoCoordinate point)
    {
        if (_disposed || !point.IsValid)
        {
            return;
        }

        _preview.CancelPending();
        AdvanceNavigation();
        long revision = _reverse.Begin();
        State = State.InspectMapPoint(point);
        await _committed();
        await NotifyMapAsync();
        if (!_reverse.IsCurrent(revision))
        {
            return;
        }

        await _render();
        await LoadReverseAsync(point, revision);
    }

    public async Task DismissMapInspectionAsync()
    {
        _backgroundClicks.CancelPending();
        if (!_disposed && State.Panel is ReverseInspectionPanel)
        {
            await BackAsync();
        }
    }

    public async Task RetryReverseAsync()
    {
        if (_disposed || State.Panel is not ReverseInspectionPanel current)
        {
            return;
        }

        long revision = _reverse.Begin();
        State = State.RetryReverse();
        await _render();
        if (_reverse.IsCurrent(revision))
        {
            await LoadReverseAsync(current.Point, revision);
        }
    }

    private async Task LoadReverseAsync(GeoCoordinate point, long revision)
    {
        SearchLoadState? status = await _reverse.FetchAsync(point, revision);
        if (_disposed || status is null)
        {
            return;
        }

        State = State.WithReverseResult(point, status);
        await _render();
    }

    /// <summary>Known marker selection never needs another HTTP request.</summary>
    public async Task SelectMapFeatureAsync(GeoJsonFeature feature)
    {
        if (_disposed)
        {
            return;
        }

        SearchMapState map = SearchMapState.FromPanel(State.Panel);
        string id = feature.Properties.Id;
        if (!map.Results.Any(result => result.Properties.Id == id) && map.SelectedFeature?.Properties.Id != id)
        {
            return;
        }

        SearchPanelState underlying = State.Panel is ReverseInspectionPanel inspection
            ? inspection.ReturnTo : State.Panel;
        bool showReturnResults = underlying is not PlaceSearchPanel current ||
            current.Feature.Properties.Id != id || current.ShowReturnResults;
        await SelectPlaceAsync(feature, showReturnResults);
    }

    public void SetMapCenter(GeoCoordinate center)
    {
        if (!_disposed && center.IsValid)
        {
            AdvanceNavigation();
            _nearbyFlow.SetMapCenter(center);
        }
    }

    public async Task OpenNearbyAsync(string? category, GeoCoordinate? selectedPoint = null)
    {
        if (_disposed)
        {
            return;
        }

        _preview.CancelPending();
        _backgroundClicks.CancelPending();
        _forward.Invalidate();
        _reverse.Invalidate();
        AdvanceNavigation();
        await _nearbyFlow.OpenNearbyAsync(category, selectedPoint);
    }

    public Task EditNearbyAsync(NearbyCriteria draft)
    {
        AdvanceNavigation();
        return _nearbyFlow.EditNearbyAsync(draft);
    }
    public Task StartEditingNearbyAsync()
    {
        AdvanceNavigation();
        return _nearbyFlow.StartEditingNearbyAsync();
    }
    public Task CancelNearbyAsync()
    {
        AdvanceNavigation();
        return _nearbyFlow.CancelNearbyAsync();
    }
    public Task FindNearbyAsync()
    {
        AdvanceNavigation();
        return _nearbyFlow.FindNearbyAsync();
    }
    public Task RetryNearbyAsync()
    {
        AdvanceNavigation();
        return _nearbyFlow.RetryNearbyAsync();
    }
    public Task SearchThisAreaAsync()
    {
        AdvanceNavigation();
        return _nearbyFlow.SearchThisAreaAsync();
    }

    private void BeforeNearbySubmit()
    {
        _reverse.Invalidate();
        _forward.Invalidate();
        AdvanceNavigation();
    }

    public Task LocateMeAsync()
    {
        if (_disposed || Locating)
        {
            return Task.CompletedTask;
        }

        AdvanceNavigation();
        return _deviceLocation.LocateAsync();
    }

    /// <summary>Apply the device-location fallback without changing applied nearby results.</summary>
    public async Task UseMapCenterAsync()
    {
        if (_disposed)
        {
            return;
        }

        AdvanceNavigation();
        if (State.Panel is NearbySearchPanel nearby)
        {
            if (!nearby.Editing)
            {
                await _nearbyFlow.StartEditingNearbyAsync();
            }

            NearbySearchPanel editor = (NearbySearchPanel)State.Panel;
            await _nearbyFlow.EditNearbyAsync(editor.Draft with { OriginKind = NearbyOriginKind.MapCenter });
        }
        else
        {
            await OpenNearbyAsync(null);
        }
    }

    private void AdvanceNavigation()
    {
        _backgroundClicks.CancelPending();
        ++_navigationRevision;
        _deviceLocation.ClearError();
    }

    private Task NotifyMapAsync() => _mapChanged(SearchMapState.FromPanel(State.Panel) with { Device = Device });

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _backgroundClicks.Dispose();
        _preview.Dispose();
        _reverse.Dispose();
        _nearbyFlow.Dispose();
        _deviceLocation.Dispose();
        _queries.Dispose();
    }
}
