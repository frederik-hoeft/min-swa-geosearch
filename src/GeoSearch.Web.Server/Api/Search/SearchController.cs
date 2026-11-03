using GeoSearch.Web.Server.Search;
using GeoSearch.Web.Shared.Api;
using GeoSearch.Web.Shared.GeoJson;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace GeoSearch.Web.Server.Api.Search;

/// <summary>The lab's anonymous, read-only search API. Location input is never persisted.</summary>
[ApiController]
[AllowAnonymous]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
[Produces("application/json")]
public sealed class SearchController(IGeoSearchService search) : ControllerBase
{
    [HttpGet("api/search")]
    [ProducesResponseType<GeoJsonFeatureCollection>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<GeoJsonFeatureCollection>> SearchAsync([FromQuery, Required] string? q, [FromQuery] int? limit, CancellationToken cancellationToken)
    {
        if (q is null or { Length: < 1 or > 200 } || string.IsNullOrWhiteSpace(q))
        {
            return BadRequest(new ApiError("q must contain between 1 and 200 characters."));
        }
        if (limit is < 1 or > 20)
        {
            return BadRequest(new ApiError("limit must be between 1 and 20."));
        }
        GeoJsonFeatureCollection results = await search.SearchAsync(q.Trim(), limit ?? 10, cancellationToken);
        return Ok(results);
    }

    [HttpGet("api/reverse")]
    [ProducesResponseType<GeoJsonFeatureCollection>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<GeoJsonFeatureCollection>> ReverseAsync([FromQuery, Required] double? lat, [FromQuery, Required] double? lon, [FromQuery] double? radius, CancellationToken cancellationToken)
    {
        if (!ValidPosition(lat, lon))
        {
            return BadRequest(new ApiError("lat must be between -90 and 90 and lon between -180 and 180."));
        }
        if (!ValidRadius(radius, 1000))
        {
            return BadRequest(new ApiError("radius must be greater than 0 and at most 1000 metres."));
        }

        return Ok(await search.ReverseAsync(lat!.Value, lon!.Value, radius ?? 250, cancellationToken));
    }

    [HttpGet("api/nearby")]
    [ProducesResponseType<GeoJsonFeatureCollection>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<GeoJsonFeatureCollection>> NearbyAsync(
        [FromQuery, Required] double? lat, [FromQuery, Required] double? lon, [FromQuery] double? radius,
        [FromQuery] string? category, [FromQuery] int? limit, CancellationToken cancellationToken)
    {
        if (!ValidPosition(lat, lon))
        {
            return BadRequest(new ApiError("lat must be between -90 and 90 and lon between -180 and 180."));
        }
        if (!ValidRadius(radius, 2000))
        {
            return BadRequest(new ApiError("radius must be greater than 0 and at most 2000 metres."));
        }
        if (limit is < 1 or > 20)
        {
            return BadRequest(new ApiError("limit must be between 1 and 20."));
        }
        if (category is { Length: > 200 } || (category is not null && string.IsNullOrWhiteSpace(category)))
        {
            return BadRequest(new ApiError("category must be a nonempty label of at most 200 characters."));
        }

        return Ok(await search.NearbyAsync(lat!.Value, lon!.Value, radius ?? 500,
            category?.Trim(), limit ?? 20, cancellationToken));
    }

    [HttpGet("api/categories")]
    [ProducesResponseType<IReadOnlyList<string>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<string>>> CategoriesAsync(CancellationToken cancellationToken) =>
        Ok(await search.CategoriesAsync(cancellationToken));

    private static bool ValidPosition(double? latitude, double? longitude) =>
        latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180;

    private static bool ValidRadius(double? radius, double max) =>
        radius is null || (radius > 0 && radius <= max);
}
