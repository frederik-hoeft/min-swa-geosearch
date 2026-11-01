using System.Net;

namespace GeoSearch.Web.Server.Security;

internal static class LoginRateLimitPolicy
{
    public const string NAME = "login";

    public static string GetClientPartition(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        IPAddress? remoteAddress = context.Connection.RemoteIpAddress;
        if (remoteAddress is null)
        {
            return "unknown";
        }

        // IPv4 and its IPv4-mapped IPv6 representation must share one partition.
        return (remoteAddress.IsIPv4MappedToIPv6 ? remoteAddress.MapToIPv4() : remoteAddress).ToString();
    }
}
