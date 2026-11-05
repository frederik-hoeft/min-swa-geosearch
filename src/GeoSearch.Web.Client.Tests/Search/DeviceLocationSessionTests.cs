using GeoSearch.Web.Client.Search.Model;
using GeoSearch.Web.Client.Services.Search;

namespace GeoSearch.Web.Client.Tests.Search;

[TestClass]
public sealed class DeviceLocationSessionTests
{
    [TestMethod]
    public async Task UserRequestedFixUpdatesDeviceAndInspectsWithoutSubmittingNearbyAsync()
    {
        GeoCoordinate point = new(52.37, 9.73);
        List<GeoCoordinate> inspected = [];
        int mapUpdates = 0;
        using DeviceLocationSession location = new(
            _ => Task.FromResult(new DeviceLocationResult(new DeviceLocationFix(point, 35), null)),
            () => Task.CompletedTask,
            () => { ++mapUpdates; return Task.CompletedTask; },
            coordinate => { inspected.Add(coordinate); return Task.CompletedTask; },
            () => 0);

        await location.LocateAsync();
        Assert.AreEqual(point, location.Device!.Point);
        Assert.AreEqual(35, location.Device.AccuracyMeters);
        Assert.AreEqual(1, mapUpdates);
        Assert.AreEqual(point, inspected.Single());
        Assert.IsFalse(location.Locating);
    }

    [TestMethod]
    public async Task PermissionDeniedDoesNotInventAFixOrStartInspectionAsync()
    {
        int inspected = 0;
        using DeviceLocationSession location = new(
            _ => Task.FromResult(new DeviceLocationResult(null, "Permission denied")),
            () => Task.CompletedTask, () => Task.CompletedTask,
            _ => { ++inspected; return Task.CompletedTask; }, () => 0);

        await location.LocateAsync();
        Assert.IsNull(location.Device);
        StringAssert.Contains(location.LocationError!, "denied", StringComparison.Ordinal);
        Assert.AreEqual(0, inspected);
    }
}
