using Community.Blazor.MapLibre;
using Community.Blazor.MapLibre.Models;
using Community.Blazor.MapLibre.Models.Feature;
using Community.Blazor.MapLibre.Models.Layers;
using Community.Blazor.MapLibre.Models.Marker;
using Community.Blazor.MapLibre.Models.Sources;
using GeoSearch.Web.Client.Search.Model;
using GeoSearch.Web.Client.Services.Theming;

namespace GeoSearch.Web.Client.UI.Components.Map;

/// <summary>
/// Renders the fixed nearby-search and device-accuracy areas as geographic
/// polygons. MapLibre owns their projection and zoom behavior; the optional
/// HTML center dots remain separate and do not intercept map clicks.
/// </summary>
internal sealed class MapRangeOverlay(MapLibre map)
{
    private const string NEARBY_SOURCE = "gs-nearby-area";
    private const string DEVICE_SOURCE = "gs-device-accuracy";
    private const string NEARBY_FILL = "gs-nearby-area-fill";
    private const string NEARBY_LINE = "gs-nearby-area-line";
    private const string DEVICE_FILL = "gs-device-area-fill";
    private const string DEVICE_LINE = "gs-device-area-line";

    private AppliedNearbyQuery? _nearby;
    private DeviceLocationFix? _device;
    private Guid? _nearbyOriginMarker;
    private Guid? _deviceOriginMarker;
    private bool _initialized;
    private bool _darkMode;

    public async Task ApplyAsync(AppliedNearbyQuery? nearby, DeviceLocationFix? device, bool isDarkMode)
    {
        if (!_initialized)
        {
            await map.AddSource(NEARBY_SOURCE, CreateSource(new FeatureCollection()));
            await map.AddSource(DEVICE_SOURCE, CreateSource(new FeatureCollection()));
            await AddLayersAsync(isDarkMode);
            _initialized = true;
            _darkMode = isDarkMode;
        }
        else if (_darkMode != isDarkMode)
        {
            await RemoveLayersAsync();
            await AddLayersAsync(isDarkMode);
            _darkMode = isDarkMode;
        }

        if (_nearby != nearby)
        {
            await map.SetSourceData(NEARBY_SOURCE, CreateSource(nearby is { IsValid: true }
                ? GeoDistanceCircle.Create(nearby.Origin, nearby.Criteria.RadiusMeters)
                : new FeatureCollection()));
            if (_nearbyOriginMarker is { } previous)
            {
                await map.RemoveMarker(previous);
                _nearbyOriginMarker = null;
            }
            if (nearby is { IsValid: true })
            {
                _nearbyOriginMarker = await AddOriginMarkerAsync(nearby.Origin, "gs-map-nearby-origin");
            }
            _nearby = nearby;
        }

        if (_device != device)
        {
            await map.SetSourceData(DEVICE_SOURCE, CreateSource(device is { IsValid: true, AccuracyMeters: > 0 }
                ? GeoDistanceCircle.Create(device.Point, device.AccuracyMeters)
                : new FeatureCollection()));
            if (_deviceOriginMarker is { } previous)
            {
                await map.RemoveMarker(previous);
                _deviceOriginMarker = null;
            }
            if (device is { IsValid: true })
            {
                _deviceOriginMarker = await AddOriginMarkerAsync(device.Point, "gs-map-device-origin");
            }
            _device = device;
        }
    }

    private async Task<Guid> AddOriginMarkerAsync(GeoCoordinate origin, string className) =>
        await map.AddMarker(new MarkerOptions
        {
            Anchor = MarkerAnchor.Center,
            ClassName = $"gs-map-range-origin {className}",
            Extensions = new MarkerOptionsExtensions
            {
                HtmlContent = "<span class=\"gs-map-range-center\" aria-hidden=\"true\"></span>"
            }
        }, new LngLat(origin.Longitude, origin.Latitude));

    internal static GeoJsonSource CreateSource(FeatureCollection collection)
    {
        IFeature feature = collection;
        return new GeoJsonSource { Data = OneOf.OneOf<IFeature, string>.FromT0(feature) };
    }

    private async Task AddLayersAsync(bool darkMode)
    {
        string primary = darkMode ? GeoSearchMudTheme.Theme.PaletteDark.Primary.ToString() : GeoSearchMudTheme.Theme.PaletteLight.Primary.ToString();
        string info = darkMode ? GeoSearchMudTheme.Theme.PaletteDark.Info.ToString() : GeoSearchMudTheme.Theme.PaletteLight.Info.ToString();
        await map.AddLayer(CreateFill(NEARBY_FILL, NEARBY_SOURCE, primary, 0.10));
        await map.AddLayer(CreateLine(NEARBY_LINE, NEARBY_SOURCE, primary));
        await map.AddLayer(CreateFill(DEVICE_FILL, DEVICE_SOURCE, info, 0.09));
        await map.AddLayer(CreateLine(DEVICE_LINE, DEVICE_SOURCE, info, dashed: true));
    }

    private async Task RemoveLayersAsync()
    {
        await map.RemoveLayer(DEVICE_LINE);
        await map.RemoveLayer(DEVICE_FILL);
        await map.RemoveLayer(NEARBY_LINE);
        await map.RemoveLayer(NEARBY_FILL);
    }

    private static FillLayer CreateFill(string id, string source, string color, double opacity) => new()
    {
        Id = id,
        Source = source,
        Paint = new FillLayerPaint { FillColor = color, FillOpacity = opacity }
    };

    private static LineLayer CreateLine(string id, string source, string color, bool dashed = false)
    {
        LineLayerPaint paint = new() { LineColor = color, LineWidth = 2 };
        if (dashed)
        {
            paint.LineDasharray = new double[] { 3.0, 2.0 };
        }
        return new LineLayer { Id = id, Source = source, Paint = paint };
    }
}
