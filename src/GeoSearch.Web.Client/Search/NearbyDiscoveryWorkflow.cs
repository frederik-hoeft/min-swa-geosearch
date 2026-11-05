using GeoSearch.Web.Client.Search.Model;
using GeoSearch.Web.Client.Services.Search;

namespace GeoSearch.Web.Client.Search;

/// <summary>
/// Nearby editor, applied-query capture, category vocabulary, and request
/// ownership. The parent workspace supplies the single typed panel state.
/// </summary>
internal sealed class NearbyDiscoveryWorkflow(SearchApiClient api, Func<ExploreSearchState> readState,
    Action<ExploreSearchState> writeState, Func<Task> render, Func<Task> committed,
    Func<Task> notifyMap, Func<Task<GeoCoordinate?>> mapCenter,
    Func<DeviceLocationFix?> device, Action beforeSubmit) : IDisposable
{
    private readonly NearbySearchSession _nearby = new(api);
    private readonly CategoryCatalogSession _categories = new(api);
    private GeoCoordinate? _manualCenter;
    private bool _disposed;

    private ExploreSearchState State
    {
        get => readState();
        set => writeState(value);
    }

    public IReadOnlyList<string> Categories => _categories.Categories;
    public string? CategoryError => _categories.Error;
    public string? NearbyError { get; private set; }
    public IReadOnlyList<string> MatchCategories(string text) => _categories.Match(text);
    public Task EnsureCategoriesAsync() => _categories.EnsureLoadedAsync(render);
    public Task RetryCategoriesAsync() => _categories.RetryAsync(render);
    public void Invalidate() => _nearby.Invalidate();
    public bool CanSearchThisArea => State.Panel is NearbySearchPanel { Applied: { } applied, Editing: false } &&
        _manualCenter is { } center && DistanceMeters(applied.Origin, center) > Math.Max(100, applied.Criteria.RadiusMeters * 0.35);

    public void SetMapCenter(GeoCoordinate center)
    {
        if (!_disposed && center.IsValid)
        {
            // User-driven map movement supersedes a pending device-navigation
            // intent but does not change any applied nearby origin or query.
            _manualCenter = center;
            _ = render();
        }
    }

    public async Task OpenNearbyAsync(string? category, GeoCoordinate? selectedPoint = null)
    {
        if (_disposed)
        {
            return;
        }

        _nearby.Invalidate();
        NearbyError = null;
        NearbyCriteria criteria = new(category, OriginKind: selectedPoint is null ? NearbyOriginKind.MapCenter : NearbyOriginKind.SelectedPoint,
            SelectedPoint: selectedPoint);
        State = State.OpenNearbyEditor(criteria);
        _ = _categories.EnsureLoadedAsync(render);
        await committed();
        await notifyMap();
        await render();
    }

    public async Task EditNearbyAsync(NearbyCriteria draft)
    {
        if (_disposed || !draft.IsValid)
        {
            return;
        }

        NearbyError = null;
        State = State.EditNearby(draft);
        await render();
    }

    public async Task StartEditingNearbyAsync()
    {
        State = State.StartEditingNearby();
        await render();
    }

    public async Task CancelNearbyAsync()
    {
        State = State.CancelNearbyEdit();
        NearbyError = null;
        await notifyMap();
        await render();
    }

    public async Task FindNearbyAsync()
    {
        if (_disposed || State.Panel is not NearbySearchPanel current)
        {
            return;
        }

        NearbyCriteria criteria = current.Draft;
        GeoCoordinate? origin = criteria.OriginKind switch
        {
            NearbyOriginKind.MapCenter => await mapCenter(),
            NearbyOriginKind.DeviceFix => device()?.Point,
            NearbyOriginKind.SelectedPoint => criteria.SelectedPoint,
            _ => null
        };
        // Map-center retrieval can await an interop round trip. Reject a
        // stale submission if the user changed criteria/panel meanwhile.
        if (_disposed || !ReferenceEquals(State.Panel, current))
        {
            return;
        }
        if (origin is not { IsValid: true })
        {
            NearbyError = criteria.OriginKind == NearbyOriginKind.DeviceFix
                ? "Locate your device first, or choose Map center."
                : "The selected map origin is unavailable. Try Map center.";
            await render();
            return;
        }

        NearbyError = null;
        beforeSubmit();
        AppliedNearbyQuery applied = new(criteria, origin.Value);
        _manualCenter = null;
        long revision = _nearby.Begin();
        State = State.CommitNearby(applied);
        await committed();
        await notifyMap();
        await render();
        await LoadNearbyAsync(applied, revision);
    }

    private async Task LoadNearbyAsync(AppliedNearbyQuery query, long revision)
    {
        SearchLoadState? status = await _nearby.FetchAsync(query, revision);
        if (_disposed || status is null)
        {
            return;
        }

        State = State.WithNearbyResult(query, status);
        await notifyMap();
        await render();
    }

    public async Task RetryNearbyAsync()
    {
        if (State.Panel is not NearbySearchPanel { Applied: { } applied } current)
        {
            return;
        }

        long revision = _nearby.Begin();
        State = State with { Panel = current with { Results = new LoadingSearch() } };
        await notifyMap();
        await render();
        await LoadNearbyAsync(applied, revision);
    }

    public async Task SearchThisAreaAsync()
    {
        if (State.Panel is not NearbySearchPanel { Applied: { } applied, Editing: false } current)
        {
            return;
        }

        GeoCoordinate? center = await mapCenter();
        if (_disposed || !ReferenceEquals(State.Panel, current) || center is not { IsValid: true })
        {
            return;
        }

        NearbyCriteria criteria = applied.Criteria with { OriginKind = NearbyOriginKind.MapCenter, SelectedPoint = null };
        AppliedNearbyQuery next = new(criteria, center.Value);
        long revision = _nearby.Begin();
        beforeSubmit();
        State = State with { Panel = current with { Draft = criteria, Applied = next, Results = new LoadingSearch() } };
        _manualCenter = null;
        await notifyMap();
        await render();
        await LoadNearbyAsync(next, revision);
    }

    private static double DistanceMeters(GeoCoordinate a, GeoCoordinate b)
    {
        const double EARTH_RADIUS = 6371000;
        double latitude = (b.Latitude - a.Latitude) * Math.PI / 180;
        double longitude = (b.Longitude - a.Longitude) * Math.PI / 180;
        double haversine = Math.Pow(Math.Sin(latitude / 2), 2) +
            Math.Cos(a.Latitude * Math.PI / 180) * Math.Cos(b.Latitude * Math.PI / 180) *
            Math.Pow(Math.Sin(longitude / 2), 2);
        return EARTH_RADIUS * 2 * Math.Asin(Math.Min(1, Math.Sqrt(haversine)));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _nearby.Dispose();
        _categories.Dispose();
    }
}
