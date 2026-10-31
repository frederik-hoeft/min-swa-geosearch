using GeoSearch.Web.Shared.Auth;
using System.ComponentModel.DataAnnotations;

namespace GeoSearch.Web.Server.Tests.Security;

[TestClass]
public sealed class LoginRequestTests
{
    [TestMethod]
    public void InvalidEmailFailsValidation()
    {
        LoginRequest request = new("bad-email", "password");
        List<ValidationResult> errors = [];
        bool valid = Validator.TryValidateObject(request, new ValidationContext(request), errors, validateAllProperties: true);
        Assert.IsFalse(valid);
    }
}
