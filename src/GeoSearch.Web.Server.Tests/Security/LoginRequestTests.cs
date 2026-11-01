using GeoSearch.Web.Shared.Auth;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace GeoSearch.Web.Server.Tests.Security;

[TestClass]
public sealed class LoginRequestTests
{
    [TestMethod]
    public void InvalidEmailFailsValidation()
    {
        bool valid = ValidateConstructorParameter(nameof(LoginRequest.Email), "bad-email");
        Assert.IsFalse(valid);
    }

    [TestMethod]
    public void ValidEmailPassesValidation()
    {
        bool valid = ValidateConstructorParameter(nameof(LoginRequest.Email), "student@example.test");
        Assert.IsTrue(valid);
    }

    [TestMethod]
    public void EmptyPasswordFailsValidation()
    {
        bool valid = ValidateConstructorParameter(nameof(LoginRequest.Password), string.Empty);
        Assert.IsFalse(valid);
    }

    private static bool ValidateConstructorParameter(string memberName, string value)
    {
        ConstructorInfo constructor = typeof(LoginRequest).GetConstructors().Single();
        ParameterInfo parameter = constructor.GetParameters().Single(candidate => candidate.Name == memberName);
        ValidationAttribute[] attributes = [.. parameter.GetCustomAttributes<ValidationAttribute>()];
        Assert.IsNotEmpty(attributes);

        LoginRequest model = new("student@example.test", "password");
        ValidationContext context = new(model) { MemberName = memberName };
        List<ValidationResult> errors = [];
        return Validator.TryValidateValue(value, context, errors, attributes);
    }
}
