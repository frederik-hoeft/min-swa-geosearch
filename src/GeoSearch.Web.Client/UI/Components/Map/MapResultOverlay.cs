using Community.Blazor.MapLibre;
using Community.Blazor.MapLibre.Models;
using Community.Blazor.MapLibre.Models.Camera;
using Community.Blazor.MapLibre.Models.Event;
using Community.Blazor.MapLibre.Models.Feature;
using Community.Blazor.MapLibre.Models.LayerFeatures;
using Community.Blazor.MapLibre.Models.Layers;
using Community.Blazor.MapLibre.Models.Sources;
using GeoSearch.Web.Client.Search.Model;
using GeoSearch.Web.Shared.GeoJson;
using OneOf;

namespace GeoSearch.Web.Client.UI.Components.Map;

/// <summary>
/// Owns the two GeoJSON sources and the MapLibre layers used for search results
/// and a foreground inspected place. Search state and HTTP never live here.
/// </summary>
internal sealed class MapResultOverlay(
    MapLibre map, Func<GeoJsonFeature, Task> onSelected, Func<Task<PointLike?>> selectionOffset)
{
    private const string RESULTS_SOURCE = "gs-search-results";
    private const string SELECTED_SOURCE = "gs-search-selection";
    private const string RESULTS_LAYER = "gs-search-result-circles";
    private const string SELECTED_LAYER = "gs-search-selected-circle";
    private const double DETAIL_ZOOM = 14.5;

    private readonly SemaphoreSlim _sync = new(1, 1);
    private SearchMapState _current = SearchMapState.Empty;
    private bool _initialized;

    public async Task InitializeAsync()
    {
        await map.AddSource(RESULTS_SOURCE, EmptySource());
        await map.AddSource(SELECTED_SOURCE, EmptySource());
        await map.AddLayer(new CircleLayer
        {
            Id = RESULTS_LAYER,
            Source = RESULTS_SOURCE,
            Paint = new CircleLayerPaint
            {
                CircleRadius = 7,
                CircleColor = "#087e6c",
                CircleStrokeColor = "#ffffff",
                CircleStrokeWidth = 2
            }
        });
        await map.AddLayer(new CircleLayer
        {
            Id = SELECTED_LAYER,
            Source = SELECTED_SOURCE,
            Paint = new CircleLayerPaint
            {
                CircleRadius = 11,
                CircleColor = "#df903b",
                CircleStrokeColor = "#122e36",
                CircleStrokeWidth = 3
            }
        });

        // Two stable subscriptions, not one .NET callback per marker per query.
        // A selected feature is excluded from the ordinary source, so a click
        // cannot select the same feature through both layer listeners.
        await map.OnClick(RESULTS_LAYER, OnMarkerClickedAsync);
        await map.OnClick(SELECTED_LAYER, OnMarkerClickedAsync);
        _initialized = true;
    }

    public async Task ApplyAsync(SearchMapState state)
    {
        await _sync.WaitAsync();
        try
        {
            if (!_initialized)
            {
                return;
            }

            SearchMapState previous = _current;
            bool selectedChanged = !SameFeature(previous.SelectedFeature, state.SelectedFeature);
            if (!ReferenceEquals(previous.Results, state.Results) || selectedChanged)
            {
                string? selectedId = state.SelectedFeature?.Properties.Id;
                await map.SetSourceData(RESULTS_SOURCE, SourceFrom(state.Results, selectedId));
                await map.SetSourceData(SELECTED_SOURCE, SourceFrom(state.SelectedFeature));
            }

            _current = state;
            if (selectedChanged && TryGetPosition(state.SelectedFeature, out LngLat? target) && target is not null)
            {
                double zoom = await map.GetZoom();
                await map.EaseTo(new EaseToOptions
                {
                    Center = target,
                    Zoom = Math.Max(zoom, DETAIL_ZOOM),
                    Duration = 450,
                    Offset = await selectionOffset()
                });
            }
        }
        finally
        {
            _sync.Release();
        }
    }

    private async Task OnMarkerClickedAsync(MapMouseEvent args)
    {
        if (args.Features.FirstOrDefault() is not LayerFeatureFeature { Id: { } id })
        {
            return;
        }

        GeoJsonFeature? feature = _current.SelectedFeature?.Properties.Id == id
            ? _current.SelectedFeature
            : _current.Results.FirstOrDefault(result => result.Properties.Id == id);
        if (feature is not null)
        {
            await onSelected(feature);
        }
    }

    private static bool SameFeature(GeoJsonFeature? first, GeoJsonFeature? second) =>
        first?.Properties.Id == second?.Properties.Id &&
        (first is null || ReferenceEquals(first, second));

    private static GeoJsonSource EmptySource() => SourceFrom([]);

    private static GeoJsonSource SourceFrom(GeoJsonFeature? feature) =>
        SourceFrom(feature is null ? [] : [feature]);

    private static GeoJsonSource SourceFrom(IReadOnlyList<GeoJsonFeature> features, string? excludeId = null) =>
        new() { Data = OneOf<IFeature, string>.FromT0(ToFeatureCollection(features, excludeId)) };

    internal static FeatureCollection ToFeatureCollection(IReadOnlyList<GeoJsonFeature> features, string? excludeId = null)
    {
        FeatureCollection result = new();
        HashSet<string> ids = new(StringComparer.Ordinal);
        foreach (GeoJsonFeature feature in features)
        {
            string id = feature.Properties.Id;
            if (string.IsNullOrWhiteSpace(id) || id == excludeId ||
                !TryGetPosition(feature, out LngLat? position) || position is null || !ids.Add(id))
            {
                continue;
            }

            result.Features.Add(new FeatureFeature
            {
                Id = id,
                Geometry = new PointGeometry { Coordinates = [position.Longitude, position.Latitude] },
                Properties = new Dictionary<string, object> { ["id"] = id }
            });
        }
        return result;
    }

    private static bool TryGetPosition(GeoJsonFeature? feature, out LngLat? position)
    {
        position = null;
        if (feature?.Geometry.Coordinates is not { Count: >= 2 } coords)
        {
            return false;
        }

        double longitude = coords[0];
        double latitude = coords[1];
        if (!double.IsFinite(longitude) || !double.IsFinite(latitude) ||
            longitude is < -180 or > 180 || latitude is < -90 or > 90)
        {
            return false;
        }

        position = new LngLat(longitude, latitude);
        return true;
    }
}
