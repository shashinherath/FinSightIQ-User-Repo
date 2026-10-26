using System.Security.Claims;

namespace FinSightIQ.UserService.Api.Middleware;

/// <summary>
/// Supports identity extraction from either direct JWT validation or gateway-forwarded
/// trusted identity headers (X-User-Id and X-User-Role).
/// </summary>
public sealed class UserContextMiddleware
{
    private const string UserIdHeader = "X-User-Id";
    private const string UserRoleHeader = "X-User-Role";

    private readonly RequestDelegate _next;

    public UserContextMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            string? headerUserId = context.Request.Headers[UserIdHeader].FirstOrDefault();
            string? headerRole = context.Request.Headers[UserRoleHeader].FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(headerUserId) && Guid.TryParse(headerUserId, out _))
            {
                List<Claim> claims = new List<Claim>
                {
                    new("sub", headerUserId),
                    new(ClaimTypes.NameIdentifier, headerUserId)
                };

                if (!string.IsNullOrWhiteSpace(headerRole))
                {
                    claims.Add(new("role", headerRole));
                    claims.Add(new(ClaimTypes.Role, headerRole));
                }

                ClaimsIdentity identity = new ClaimsIdentity(claims, "GatewayTrustedIdentity");
                context.User = new ClaimsPrincipal(identity);
            }
        }

        await _next(context);
    }
}
