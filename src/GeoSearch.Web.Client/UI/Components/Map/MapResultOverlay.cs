using Community.Blazor.MapLibre;
using Community.Blazor.MapLibre.Models;
using Community.Blazor.MapLibre.Models.Camera;
using Community.Blazor.MapLibre.Models.Marker;
using GeoSearch.Web.Client.Search.Model;
using GeoSearch.Web.Shared.GeoJson;
using System.Text.Encodings.Web;

namespace GeoSearch.Web.Client.UI.Components.Map;

/// <summary>
/// Reconciles the committed search projection with MapLibre's HTML markers.
/// No HTTP requests or search-state transitions are owned by this class.
/// </summary>
internal sealed class MapResultOverlay(
    MapLibre map, Func<GeoJsonFeature, Task> onSelected, Func<Task<double>> selectionOffsetPixels)
{
    private const double DETAIL_ZOOM = 14.5;

    private readonly SemaphoreSlim _sync = new(1, 1);
    private readonly Dictionary<string, (Guid MarkerId, GeoJsonFeature Feature, bool Selected)> _markers = new(StringComparer.Ordinal);
    private SearchMapState _current = SearchMapState.Empty;
    private Guid? _inspectedPointMarker;

    public async Task ApplyAsync(SearchMapState state)
    {
        await _sync.WaitAsync();
        try
        {
            // A single marker per feature ID: the selected marker replaces its
            // normal counterpart instead of appearing twice at the same point.
            Dictionary<string, (GeoJsonFeature Feature, bool Selected)> desired = ProjectMarkers(state);
            foreach (KeyValuePair<string, (Guid MarkerId, GeoJsonFeature Feature, bool Selected)> current in _markers.ToArray())
            {
                if (!desired.TryGetValue(current.Key, out (GeoJsonFeature Feature, bool Selected) next) ||
                    !SameAppearance(current.Value.Feature, current.Value.Selected, next.Feature, next.Selected))
                {
                    await map.RemoveMarker(current.Value.MarkerId);
                    _markers.Remove(current.Key);
                }
            }

            foreach (KeyValuePair<string, (GeoJsonFeature Feature, bool Selected)> item in desired)
            {
                if (_markers.ContainsKey(item.Key) || !TryGetPosition(item.Value.Feature, out LngLat? point) || point is null)
                {
                    continue;
                }

                string featureId = item.Key;
                MarkerOptions options = CreateMarkerOptions(item.Value.Feature, item.Value.Selected, () => OnMarkerClickedAsync(featureId));
                Guid markerId = await map.AddMarker(options, point);
                _markers.Add(featureId, (markerId, item.Value.Feature, item.Value.Selected));
            }

            if (_current.InspectionPoint != state.InspectionPoint)
            {
                if (_inspectedPointMarker is Guid previous)
                {
                    await map.RemoveMarker(previous);
                    _inspectedPointMarker = null;
                }
                if (state.InspectionPoint is { } inspected && inspected.IsValid)
                {
                    _inspectedPointMarker = await map.AddMarker(new MarkerOptions
                    {
                        Anchor = MarkerAnchor.Center,
                        ClassName = "gs-map-inspection-marker",
                        Extensions = new MarkerOptionsExtensions
                        {
                            HtmlContent = "<span class=\"gs-map-inspection-point\" aria-hidden=\"true\"></span>"
                        }
                    }, new LngLat(inspected.Longitude, inspected.Latitude));
                }
            }

            bool selectionChanged = !SameFeature(_current.SelectedFeature, state.SelectedFeature);
            _current = state;
            if (selectionChanged && TryGetPosition(state.SelectedFeature, out LngLat? target) && target is not null)
            {
                double zoom = await map.GetZoom();
                double targetZoom = Math.Max(zoom, DETAIL_ZOOM);
                double upwardOffsetPixels = await selectionOffsetPixels();
                await map.EaseTo(new EaseToOptions
                {
                    // The wrapper's PointLike is serialized as {x,y}, whereas
                    // MapLibre's easeTo offset expects a point-geometry value.
                    // Move the geographic camera center instead: no persistent
                    // padding and no additional JavaScript interoperability.
                    Center = SelectionCamera.GetCenter(target, targetZoom, upwardOffsetPixels),
                    Zoom = targetZoom,
                    Duration = 450
                });
            }
        }
        finally
        {
            _sync.Release();
        }
    }

    private async Task OnMarkerClickedAsync(string id)
    {
        // An event already in flight must not select a marker removed by a
        // newer committed query.
        if (!_markers.ContainsKey(id))
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

    internal static Dictionary<string, (GeoJsonFeature Feature, bool Selected)> ProjectMarkers(SearchMapState state)
    {
        Dictionary<string, (GeoJsonFeature Feature, bool Selected)> projected = new(StringComparer.Ordinal);
        foreach (GeoJsonFeature feature in state.Results)
        {
            if (!string.IsNullOrWhiteSpace(feature.Properties.Id) &&
                TryGetPosition(feature, out _))
            {
                projected.TryAdd(feature.Properties.Id, (feature, false));
            }
        }

        if (state.SelectedFeature is { } selected && !string.IsNullOrWhiteSpace(selected.Properties.Id) &&
            TryGetPosition(selected, out _))
        {
            projected[selected.Properties.Id] = (selected, true);
        }

        return projected;
    }

    internal static MarkerOptions CreateMarkerOptions(GeoJsonFeature feature, bool selected, Func<Task> onClick)
    {
        string label = HtmlEncoder.Default.Encode(feature.Properties.Label);
        return new MarkerOptions
        {
            Anchor = MarkerAnchor.Bottom,
            ClassName = selected ? "gs-search-marker gs-search-marker--selected" : "gs-search-marker",
            Extensions = new MarkerOptionsExtensions
            {
                // This is a real button, with native keyboard activation and an
                // accessible label. User-provided labels are HTML-encoded.
                HtmlContent = $"<button class=\"gs-map-pin\" type=\"button\" aria-label=\"Show {label} on map\" title=\"{label}\"><svg viewBox=\"0 0 36 48\" aria-hidden=\"true\" focusable=\"false\"><path class=\"gs-map-pin__shape\" d=\"M18 1.5C9 1.5 1.5 9 1.5 18c0 11.2 12 24.5 16.5 28.5C22.5 42.5 34.5 29.2 34.5 18 34.5 9 27 1.5 18 1.5Z\"/><circle class=\"gs-map-pin__center\" cx=\"18\" cy=\"18\" r=\"6\"/></svg></button>"
            },
            // The wrapper's onMarker click path serializes a small payload,
            // unlike the map-level event handler that serializes a circular Map.
            OnClickAsync = _ => onClick()
        };
    }

    private static bool SameAppearance(GeoJsonFeature first, bool firstSelected, GeoJsonFeature second, bool secondSelected) =>
        firstSelected == secondSelected &&
        first.Properties.Label == second.Properties.Label &&
        first.Geometry.Coordinates.SequenceEqual(second.Geometry.Coordinates);

    private static bool SameFeature(GeoJsonFeature? first, GeoJsonFeature? second) =>
        first?.Properties.Id == second?.Properties.Id &&
        (first is null || ReferenceEquals(first, second));

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
