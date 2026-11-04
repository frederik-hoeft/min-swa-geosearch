using Community.Blazor.MapLibre;
using Community.Blazor.MapLibre.Models;
using Community.Blazor.MapLibre.Models.Camera;
using Community.Blazor.MapLibre.Models.Feature;
using Community.Blazor.MapLibre.Models.Image;
using Community.Blazor.MapLibre.Models.Layers;
using Community.Blazor.MapLibre.Models.Sources;
using GeoSearch.Web.Client.Search.Model;
using GeoSearch.Web.Shared.GeoJson;
using OneOf;
using System.Text.Json;

namespace GeoSearch.Web.Client.UI.Components.Map;

/// <summary>
/// Owns the two GeoJSON sources and the MapLibre layers used for search results
/// and a foreground inspected place. Search state and HTTP never live here.
/// </summary>
internal sealed class MapResultOverlay(
    MapLibre map, Func<GeoJsonFeature, Task> onSelected, Func<Task<PointLike?>> selectionOffset,
    Func<bool, Task> onHoverChanged, string resultIconUrl, string selectedIconUrl)
{
    private const string RESULTS_SOURCE = "gs-search-results";
    private const string SELECTED_SOURCE = "gs-search-selection";
    private const string RESULTS_LAYER = "gs-search-result-pins";
    private const string SELECTED_LAYER = "gs-search-selected-pin";
    private const string RESULTS_ICON = "gs-search-pin-image";
    private const string SELECTED_ICON = "gs-search-selected-image";
    private const double DETAIL_ZOOM = 14.5;

    private readonly SemaphoreSlim _sync = new(1, 1);
    private SearchMapState _current = SearchMapState.Empty;
    private bool _initialized;

    public async Task InitializeAsync()
    {
        // Blazor.MapLibre can load PNG icon assets directly into its sprite registry.
        // Keep the features as GeoJSON rather than creating DOM markers/callbacks
        // for each result; both sources and listeners survive query changes.
        StyleImageMetadata imageMetadata = new() { PixelRatio = 2 };
        await map.AddImage(RESULTS_ICON, resultIconUrl, imageMetadata);
        await map.AddImage(SELECTED_ICON, selectedIconUrl, imageMetadata);
        await map.AddSource(RESULTS_SOURCE, EmptySource());
        await map.AddSource(SELECTED_SOURCE, EmptySource());
        await map.AddLayer(new SymbolLayer
        {
            Id = RESULTS_LAYER,
            Source = RESULTS_SOURCE,
            Layout = PinLayout(RESULTS_ICON)
        });
        await map.AddLayer(new SymbolLayer
        {
            Id = SELECTED_LAYER,
            Source = SELECTED_SOURCE,
            Layout = PinLayout(SELECTED_ICON, 0.65)
        });

        // The wrapper serializes browser events before handing them to .NET.
        // Read the minimal ID from JSON rather than binding the entire event
        // (including optional vendor-specific fields) to a rigid .NET model.
        await map.AddAsyncListener<JsonElement>("click", OnMarkerClickedAsync, RESULTS_LAYER);
        await map.AddAsyncListener<JsonElement>("click", OnMarkerClickedAsync, SELECTED_LAYER);
        await map.AddAsyncListener<JsonElement>("mouseenter", OnHoverEnteredAsync, RESULTS_LAYER);
        await map.AddAsyncListener<JsonElement>("mouseenter", OnHoverEnteredAsync, SELECTED_LAYER);
        await map.AddAsyncListener<JsonElement>("mouseleave", OnHoverLeftAsync, RESULTS_LAYER);
        await map.AddAsyncListener<JsonElement>("mouseleave", OnHoverLeftAsync, SELECTED_LAYER);
        _initialized = true;
    }

    private static SymbolLayerLayout PinLayout(string iconId, double size = 0.5) => new()
    {
        IconImage = iconId,
        IconSize = size,
        IconAnchor = "bottom",
        IconAllowOverlap = true,
        IconIgnorePlacement = true
    };

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

    private async Task OnMarkerClickedAsync(JsonElement clicked)
    {
        string? id = ClickedFeatureId(clicked);
        if (id is null)
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

    private Task OnHoverEnteredAsync(JsonElement _) => onHoverChanged(true);

    private Task OnHoverLeftAsync(JsonElement _) => onHoverChanged(false);

    internal static string? ClickedFeatureId(JsonElement clicked)
    {
        if (clicked.ValueKind != JsonValueKind.Object ||
            !clicked.TryGetProperty("features", out JsonElement features) ||
            features.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (JsonElement feature in features.EnumerateArray())
        {
            if (feature.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            // Our API ID is mirrored in properties.id. Prefer it to the
            // renderer's feature.id, which some GeoJSON pipelines may coerce.
            if (feature.TryGetProperty("properties", out JsonElement properties) &&
                properties.ValueKind == JsonValueKind.Object &&
                properties.TryGetProperty("id", out JsonElement propertyId) &&
                ReadId(propertyId) is { Length: > 0 } id)
            {
                return id;
            }

            if (feature.TryGetProperty("id", out JsonElement featureId) &&
                ReadId(featureId) is { Length: > 0 } fallbackId)
            {
                return fallbackId;
            }
        }

        return null;
    }

    private static string? ReadId(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString(),
        JsonValueKind.Number => value.GetRawText(),
        _ => null
    };

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
