using System.Security.Claims;
using System.Text.Json;
using FinSightIQ.BuildingBlocks.Middleware;
using FinSightIQ.UserService.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace FinSightIQ.UserService.Api.Extensions;

public static class AuthenticationExtensions
{
    public const string ViewerPolicy = "ViewerPolicy";
    public const string AnalystPolicy = "AnalystPolicy";
    public const string AdminPolicy = "AdminPolicy";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IServiceCollection AddUserServiceAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        IConfigurationSection jwtSection = configuration.GetSection(JwtOptions.SectionName);
        JwtOptions jwtOptions = jwtSection.Get<JwtOptions>() ?? new JwtOptions();

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.RequireHttpsMetadata = false;
            options.SaveToken = false;

            // Resolve validation key dynamically from DI
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    IRsaKeyProvider keyProvider = context.HttpContext.RequestServices.GetRequiredService<IRsaKeyProvider>();
                    RsaSecurityKey validationKey = keyProvider.ValidationKey;

                    context.Options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = jwtOptions.Issuer,
                        ValidateAudience = true,
                        ValidAudience = jwtOptions.Audience,
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = validationKey,
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.FromSeconds(60),
                        ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
                        RequireSignedTokens = true,
                        RoleClaimType = "role"
                    };

                    return Task.CompletedTask;
                },
                OnChallenge = async context =>
                {
                    context.HandleResponse();

                    HttpContext httpContext = context.HttpContext;
                    httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    httpContext.Response.ContentType = "application/problem+json; charset=utf-8";

                    bool isExpired = context.AuthenticateFailure is SecurityTokenExpiredException
                        || (context.ErrorDescription != null && context.ErrorDescription.Contains("expired", StringComparison.OrdinalIgnoreCase))
                        || (context.AuthenticateFailure?.Message != null && context.AuthenticateFailure.Message.Contains("expired", StringComparison.OrdinalIgnoreCase));

                    string code = isExpired ? "TOKEN_EXPIRED" : "UNAUTHENTICATED";
                    string title = isExpired ? "Access token expired" : "Authentication failed";
                    string detail = isExpired
                        ? "Access token past expiry. Please use your refresh token to acquire a new access token."
                        : (context.ErrorDescription ?? "Missing, invalid or unverified access token.");

                    string traceId = httpContext.Items[CorrelationId.HeaderName]?.ToString()
                        ?? httpContext.TraceIdentifier;

                    object problem = new
                    {
                        type = isExpired ? "https://finsightiq.io/errors/token-expired" : "https://finsightiq.io/errors/unauthenticated",
                        title,
                        status = StatusCodes.Status401Unauthorized,
                        code,
                        detail,
                        instance = httpContext.Request.Path.Value,
                        traceId,
                        errors = (object?)null
                    };

                    await httpContext.Response.WriteAsync(JsonSerializer.Serialize(problem, JsonOptions));
                }
            };
        });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(ViewerPolicy, policy =>
                policy.RequireAssertion(ctx => HasAnyRole(ctx.User, "Viewer", "Analyst", "Administrator")));

            options.AddPolicy(AnalystPolicy, policy =>
                policy.RequireAssertion(ctx => HasAnyRole(ctx.User, "Analyst", "Administrator")));

            options.AddPolicy(AdminPolicy, policy =>
                policy.RequireAssertion(ctx => HasAnyRole(ctx.User, "Administrator")));
        });

        return services;
    }

    private static bool HasAnyRole(ClaimsPrincipal principal, params string[] allowedRoles)
    {
        string? role = principal.FindFirst("role")?.Value
            ?? principal.FindFirst(ClaimTypes.Role)?.Value;

        if (string.IsNullOrWhiteSpace(role))
        {
            return false;
        }

        return allowedRoles.Any(r => r.Equals(role, StringComparison.OrdinalIgnoreCase));
    }
}
