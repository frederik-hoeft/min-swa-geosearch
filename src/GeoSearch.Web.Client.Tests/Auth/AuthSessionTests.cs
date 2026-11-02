using GeoSearch.Web.Client.Auth;
using GeoSearch.Web.Shared.Auth;

namespace GeoSearch.Web.Client.Tests.Auth;

[TestClass]
public sealed class AuthSessionTests
{
    [TestMethod]
    public void SessionTracksAccessTokenSeparatelyFromItsExpiry()
    {
        AuthSession session = new();
        session.Set(new LoginResponse("token", DateTimeOffset.UtcNow.AddMinutes(5), "student@example.test"));
        Assert.IsTrue(session.IsAuthenticated);
        Assert.AreEqual("student@example.test", session.Email);

        session.Clear();
        Assert.IsFalse(session.IsAuthenticated);
        Assert.IsNull(session.AccessToken);
        Assert.IsNull(session.Email);

        session.Set(new LoginResponse("expired", DateTimeOffset.UtcNow.AddSeconds(-1), "student@example.test"));
        Assert.IsTrue(session.IsAuthenticated);
        Assert.IsTrue(session.ExpiresUtc < DateTimeOffset.UtcNow);
    }
}
