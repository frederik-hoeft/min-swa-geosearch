using GeoSearch.Web.Server.Security;
using Microsoft.AspNetCore.Http;
using System.Net;

namespace GeoSearch.Web.Server.Tests.Security;

[TestClass]
public sealed class LoginRateLimitPolicyTests
{
    [TestMethod]
    public void SeparateClientAddressesHaveSeparatePartitions()
    {
        DefaultHttpContext clientA = new();
        clientA.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.10");
        DefaultHttpContext clientB = new();
        clientB.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.11");

        Assert.AreNotEqual(LoginRateLimitPolicy.GetClientPartition(clientA), LoginRateLimitPolicy.GetClientPartition(clientB));
    }

    [TestMethod]
    public void Ipv4MappedIpv6SharesIpv4Partition()
    {
        DefaultHttpContext ipv4 = new();
        ipv4.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.10");
        DefaultHttpContext mapped = new();
        mapped.Connection.RemoteIpAddress = IPAddress.Parse("::ffff:192.0.2.10");

        Assert.AreEqual(LoginRateLimitPolicy.GetClientPartition(ipv4), LoginRateLimitPolicy.GetClientPartition(mapped));
    }

    [TestMethod]
    public void ClientSuppliedForwardingHeaderCannotChoosePartition()
    {
        DefaultHttpContext context = new();
        context.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.10");
        context.Request.Headers["X-Forwarded-For"] = "198.51.100.7";

        Assert.AreEqual("192.0.2.10", LoginRateLimitPolicy.GetClientPartition(context));
    }
}
