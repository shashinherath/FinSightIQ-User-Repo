using System.Security.Claims;
using FinSightIQ.BuildingBlocks.Middleware;
using FinSightIQ.BuildingBlocks.Results;
using Microsoft.AspNetCore.Mvc;

namespace FinSightIQ.UserService.Api.Extensions;

public static class ProblemDetailsExtensions
{
    public static Guid? GetUserId(this ClaimsPrincipal principal)
    {
        string? sub = principal.FindFirst("sub")?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return Guid.TryParse(sub, out Guid guid) ? guid : null;
    }

    public static IActionResult ToProblemResult(
        this ControllerBase controller,
        Error error,
        IDictionary<string, string[]>? validationErrors = null)
    {
        int statusCode = error.Code switch
        {
            "VALIDATION_FAILED" => StatusCodes.Status400BadRequest,
            "MALFORMED_REQUEST" => StatusCodes.Status400BadRequest,
            "UNAUTHENTICATED" => StatusCodes.Status401Unauthorized,
            "TOKEN_EXPIRED" => StatusCodes.Status401Unauthorized,
            "TOKEN_REUSE_DETECTED" => StatusCodes.Status401Unauthorized,
            "FORBIDDEN" => StatusCodes.Status403Forbidden,
            "ACCOUNT_SUSPENDED" => StatusCodes.Status403Forbidden,
            "RESOURCE_NOT_FOUND" => StatusCodes.Status404NotFound,
            "EMAIL_ALREADY_REGISTERED" => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError
        };

        string type = error.Code switch
        {
            "VALIDATION_FAILED" => "https://finsightiq.io/errors/validation-failed",
            "UNAUTHENTICATED" => "https://finsightiq.io/errors/unauthenticated",
            "TOKEN_EXPIRED" => "https://finsightiq.io/errors/token-expired",
            "TOKEN_REUSE_DETECTED" => "https://finsightiq.io/errors/token-reuse-detected",
            "FORBIDDEN" => "https://finsightiq.io/errors/forbidden",
            "ACCOUNT_SUSPENDED" => "https://finsightiq.io/errors/account-suspended",
            "RESOURCE_NOT_FOUND" => "https://finsightiq.io/errors/resource-not-found",
            "EMAIL_ALREADY_REGISTERED" => "https://finsightiq.io/errors/email-already-registered",
            _ => "https://finsightiq.io/errors/internal-error"
        };

        string title = error.Code switch
        {
            "VALIDATION_FAILED" => "Validation failed",
            "UNAUTHENTICATED" => "Authentication failed",
            "TOKEN_EXPIRED" => "Access token expired",
            "TOKEN_REUSE_DETECTED" => "Token reuse detected",
            "FORBIDDEN" => "Access forbidden",
            "ACCOUNT_SUSPENDED" => "Account suspended",
            "RESOURCE_NOT_FOUND" => "Resource not found",
            "EMAIL_ALREADY_REGISTERED" => "Email already registered",
            _ => "Internal server error"
        };

        string traceId = controller.HttpContext.Items[CorrelationId.HeaderName]?.ToString()
            ?? controller.HttpContext.TraceIdentifier;

        object problem = new
        {
            type,
            title,
            status = statusCode,
            code = error.Code,
            detail = error.Message,
            instance = controller.HttpContext.Request.Path.Value,
            traceId,
            errors = validationErrors
        };

        return new ObjectResult(problem)
        {
            StatusCode = statusCode,
            ContentTypes = { "application/problem+json; charset=utf-8" }
        };
    }
}
