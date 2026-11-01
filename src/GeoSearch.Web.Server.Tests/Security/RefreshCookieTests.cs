using GeoSearch.Web.Server.Security;
using Microsoft.AspNetCore.Http;

namespace GeoSearch.Web.Server.Tests.Security;

[TestClass]
public sealed class RefreshCookieTests
{
    [TestMethod]
    public void CookieIsHttpOnlySecureHostScopedAndStrictSameSite()
    {
        DateTimeOffset until = DateTimeOffset.UtcNow.AddDays(30);
        CookieOptions cookie = RefreshTokenCookiePolicy.Create(until);

        Assert.IsTrue(cookie.HttpOnly);
        Assert.IsTrue(cookie.Secure);
        Assert.AreEqual(SameSiteMode.Strict, cookie.SameSite);
        Assert.AreEqual("/", cookie.Path);
        Assert.IsNull(cookie.Domain);
        Assert.AreEqual(until, cookie.Expires);
    }

    [TestMethod]
    public void DeletionUsesSameCookieSecurityAttributes()
    {
        CookieOptions cookie = RefreshTokenCookiePolicy.Create();
        Assert.IsTrue(cookie.HttpOnly);
        Assert.IsTrue(cookie.Secure);
        Assert.AreEqual(SameSiteMode.Strict, cookie.SameSite);
        Assert.IsNull(cookie.Expires);
    }
}
