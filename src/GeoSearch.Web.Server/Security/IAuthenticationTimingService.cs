namespace GeoSearch.Web.Server.Security;

internal interface IAuthenticationTimingService
{
    void PerformDummyPasswordVerification(string suppliedPassword);
}
