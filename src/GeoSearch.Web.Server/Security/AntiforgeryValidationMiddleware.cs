using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;

namespace GeoSearch.Web.Server.Security;

/// <summary>Validates antiforgery tokens only for endpoints explicitly requiring cookie CSRF protection.</summary>
internal sealed partial class AntiforgeryValidationMiddleware(
    RequestDelegate next,
    IAntiforgery antiforgery,
    IProblemDetailsService problemDetails,
    ILogger<AntiforgeryValidationMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.GetEndpoint()?.Metadata.GetMetadata<RequireAntiforgeryValidationAttribute>() is null)
        {
            await next(context);
            return;
        }

        try
        {
            await antiforgery.ValidateRequestAsync(context);
        }
        catch (AntiforgeryValidationException exception)
        {
            LogInvalidToken(logger, exception.Message, exception);
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            ProblemDetails problem = new()
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Antiforgery validation failed",
                Detail = "The antiforgery token is invalid or missing.",
            };
            await problemDetails.WriteAsync(new ProblemDetailsContext
            {
                HttpContext = context,
                ProblemDetails = problem,
            });
            return;
        }

        await next(context);
    }

    [LoggerMessage(1, LogLevel.Information, "Antiforgery token validation failed. {Message}", EventName = "AntiforgeryTokenInvalid")]
    private static partial void LogInvalidToken(ILogger logger, string message, Exception exception);
}
