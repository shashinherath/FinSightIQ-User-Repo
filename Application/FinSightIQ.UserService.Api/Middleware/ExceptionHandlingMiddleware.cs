using System.Net;
using System.Text.Json;
using FinSightIQ.BuildingBlocks.Exceptions;

namespace FinSightIQ.UserService.Api.Middleware;

public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    private readonly RequestDelegate _next = next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger = logger;

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await LogExceptionDetailsAsync(context, ex);
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task LogExceptionDetailsAsync(HttpContext context, Exception exception)
    {
        // Capture request body for logging (if available and not too large)
        string? requestBody = null;
        if (context.Request.Body.CanSeek && context.Request.ContentLength < 10240) // 10KB limit
        {
            try
            {
                context.Request.Body.Position = 0;
                using StreamReader reader = new StreamReader(context.Request.Body);
                requestBody = await reader.ReadToEndAsync();
                context.Request.Body.Position = 0; // Reset for potential retry
            }
            catch
            {
                requestBody = "[Unable to read request body]";
            }
        }

        // Extract user information if available
        string? userId = context.User?.FindFirst("sub")?.Value ?? 
                    context.User?.FindFirst("id")?.Value ?? 
                    context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        
        string? userName = context.User?.FindFirst("name")?.Value ?? 
                      context.User?.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;

        string? companyId = context.User?.FindFirst("company_id")?.Value;

        // Get request headers (excluding sensitive ones)
        Dictionary<string, string> sanitizedHeaders = context.Request.Headers
            .Where(h => !IsSensitiveHeader(h.Key))
            .ToDictionary(h => h.Key, h => h.Value.ToString());

        // Create structured log data
        using IDisposable? logScope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["RequestId"] = context.TraceIdentifier,
            ["CorrelationId"] = context.Request.Headers["X-Correlation-ID"].FirstOrDefault()
                ?? context.Items["CorrelationId"]?.ToString(),
            ["UserId"] = userId,
            ["UserName"] = userName,
            ["CompanyId"] = companyId,
            ["RequestPath"] = context.Request.Path.Value,
            ["RequestMethod"] = context.Request.Method,
            ["RequestQuery"] = context.Request.QueryString.Value,
            ["RequestHeaders"] = sanitizedHeaders,
            ["RequestBody"] = requestBody,
            ["UserAgent"] = context.Request.Headers.UserAgent.ToString(),
            ["RemoteIpAddress"] = context.Connection.RemoteIpAddress?.ToString(),
            ["RequestTimestamp"] = DateTime.UtcNow,
            ["ExceptionType"] = exception.GetType().Name,
            ["ExceptionSource"] = exception.Source,
            ["ExceptionTargetSite"] = exception.TargetSite?.Name
        });

        // Log with appropriate level based on exception type
        LogLevel logLevel = GetLogLevelForException(exception);
        
        _logger.Log(logLevel, exception, 
            "Unhandled exception occurred. " +
            "Method: {Method} | Path: {Path} | User: {UserId} | " +
            "Exception: {ExceptionType} | Message: {Message}",
            context.Request.Method,
            context.Request.Path.Value,
            userId ?? "Anonymous",
            exception.GetType().Name,
            exception.Message);

        // Log additional details for specific exception types
        switch (exception)
        {
            case DomainException dex:
                _logger.LogWarning("Domain business rule violated - Code: {Code} | Message: {Message}",
                    dex.Code, dex.Message);
                break;

            case UnauthorizedAccessException:
                _logger.LogWarning("Authorization failed - User: {UserId} | Path: {Path} | " +
                                  "Required Roles: {RequiredRoles}",
                    userId ?? "Anonymous", context.Request.Path.Value,
                    exception.Data["RequiredRoles"] ?? "Unknown");
                break;

            case ArgumentException argEx when argEx.ParamName != null:
                _logger.LogWarning("Invalid argument provided - Parameter: {Parameter} | " +
                                  "Value: {Value}", argEx.ParamName, argEx.Data["Value"] ?? "Unknown");
                break;
        }

        // Log stack trace for server errors
        if (exception is not (KeyNotFoundException or UnauthorizedAccessException or ArgumentException or DomainException))
        {
            _logger.LogError("Full stack trace: {StackTrace}", exception.StackTrace);
            
            // Log inner exceptions if present
            Exception? innerEx = exception.InnerException;
            int innerLevel = 1;
            while (innerEx != null && innerLevel <= 3) // Limit to 3 levels
            {
                _logger.LogError("Inner Exception {Level}: {Type} - {Message}", 
                    innerLevel, innerEx.GetType().Name, innerEx.Message);
                innerEx = innerEx.InnerException;
                innerLevel++;
            }
        }
    }

    private static LogLevel GetLogLevelForException(Exception exception)
    {
        return exception switch
        {
            KeyNotFoundException => LogLevel.Information, // These are often expected (e.g., invalid IDs)
            DomainException => LogLevel.Warning,          // Domain business rules
            ArgumentException => LogLevel.Warning,        // Validation failures
            UnauthorizedAccessException => LogLevel.Warning, // Auth failures
            InvalidOperationException => LogLevel.Error,  // Logic errors
            TimeoutException => LogLevel.Error,           // Performance issues
            _ => LogLevel.Critical                        // Unknown/unexpected errors
        };
    }

    private static bool IsSensitiveHeader(string headerName)
    {
        string[] sensitiveHeaders = new[]
        {
            "authorization", "cookie", "x-api-key", "x-auth-token", 
            "x-forwarded-for", "x-real-ip", "proxy-authorization"
        };
        
        return sensitiveHeaders.Contains(headerName.ToLowerInvariant());
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        HttpResponse response = context.Response;
        response.ContentType = "application/json";

        (HttpStatusCode statusCode, string message) = exception switch
        {
            KeyNotFoundException => (HttpStatusCode.NotFound, exception.Message),
            DomainException dex => (HttpStatusCode.UnprocessableEntity, dex.Message),
            ArgumentException => (HttpStatusCode.BadRequest, exception.Message),
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized, "Access denied"),
            TimeoutException => (HttpStatusCode.RequestTimeout, "The operation timed out"),
            _ => (HttpStatusCode.InternalServerError, "An internal server error occurred")
        };

        response.StatusCode = (int)statusCode;

        object errorResponse = new
        {
            error = message,
            requestId = context.TraceIdentifier
        };

        string jsonResponse = JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await response.WriteAsync(jsonResponse);
    }
}
