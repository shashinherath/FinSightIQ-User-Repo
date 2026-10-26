using System.Data;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using FinSightIQ.UserService.Api.Extensions;
using FinSightIQ.UserService.Api.Health;
using FinSightIQ.UserService.Api.Middleware;
using FinSightIQ.UserService.Core.DTOs;
using FinSightIQ.UserService.Core.Interfaces;
using FinSightIQ.UserService.Core.Services;
using FinSightIQ.UserService.Core.Validators;
using FinSightIQ.UserService.Infrastructure.Persistence;
using FinSightIQ.UserService.Infrastructure.Repositories;
using FinSightIQ.UserService.Infrastructure.Security;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.OpenApi.Models;
using Npgsql;
using Serilog;

namespace FinSightIQ.UserService.Api;

public class Program
{
    public static void Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .CreateBootstrapLogger();

        try
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

            // Configure Serilog with Host integration
            builder.Host.UseSerilog((context, services, configuration) =>
            {
                configuration
                    .ReadFrom.Configuration(context.Configuration)
                    .ReadFrom.Services(services)
                    .Enrich.FromLogContext()
                    .WriteTo.Console();
            });

            // Add services to the container
            builder.Services.AddControllers()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                    options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
                    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
                });

            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo
                {
                    Version = "v1",
                    Title = "FinSightIQ User Service API",
                    Description = "An ASP.NET Core Web API for FinSightIQ User Module"
                });

                options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = SecuritySchemeType.Http,
                    Scheme = "Bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Description = "Enter your JWT Bearer token."
                });

                options.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            }
                        },
                        Array.Empty<string>()
                    }
                });

                string xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                string xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFilename);
                if (File.Exists(xmlPath))
                {
                    options.IncludeXmlComments(xmlPath);
                }
            });

            // Register database connection
            builder.Services.AddScoped<IDbConnection>(sp =>
            {
                string? connectionString = builder.Configuration.GetConnectionString("UserDb")
                    ?? builder.Configuration.GetConnectionString("DefaultConnection");
                if (string.IsNullOrEmpty(connectionString))
                {
                    throw new InvalidOperationException("Database connection string is not configured.");
                }
                return new NpgsqlConnection(connectionString);
            });

            // Register Dapper wrapper
            builder.Services.AddScoped<IDapperWrapper, DapperWrapper>();

            // Register repositories
            builder.Services.AddScoped<IUserRepository, UserRepository>();
            builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
            builder.Services.AddScoped<IWatchlistRepository, WatchlistRepository>();

            // Register services
            builder.Services.AddScoped<IUserService, Core.Services.UserService>();
            builder.Services.AddScoped<IWatchlistService, WatchlistService>();

            builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
            builder.Services.AddSingleton<IRsaKeyProvider, RsaKeyProvider>();
            builder.Services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
            builder.Services.AddSingleton<ITokenService, JwtTokenService>();

            // Register Validators
            builder.Services.AddSingleton<IValidator<RegisterRequest>, RegisterRequestValidator>();
            builder.Services.AddSingleton<IValidator<LoginRequest>, LoginRequestValidator>();
            builder.Services.AddSingleton<IValidator<ChangePasswordRequest>, ChangePasswordRequestValidator>();
            builder.Services.AddSingleton<IValidator<WatchlistAddRequest>, WatchlistAddRequestValidator>();

            // Register Authentication & Authorization
            builder.Services.AddUserServiceAuthentication(builder.Configuration);

            // Register Health Checks
            builder.Services.AddHealthChecks()
                .AddCheck<DatabaseHealthCheck>("database", tags: new[] { "ready" });

            WebApplication app = builder.Build();

            // Middleware
            app.UseMiddleware<CorrelationIdMiddleware>();
            app.UseSerilogRequestLogging();
            app.UseMiddleware<ExceptionHandlingMiddleware>();

            // Configure the HTTP request pipeline
            if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Swagger:Enabled", true))
            {
                app.UseSwagger();
                app.UseSwaggerUI(c =>
                {
                    c.SwaggerEndpoint("/swagger/v1/swagger.json", "FinSightIQ User Service API v1");
                    c.RoutePrefix = "swagger";
                });
            }

            app.UseHttpsRedirection();
            app.UseAuthentication();
            app.UseMiddleware<UserContextMiddleware>();
            app.UseAuthorization();

            // Health Check Endpoints
            app.MapHealthChecks("/health", new HealthCheckOptions
            {
                ResultStatusCodes =
                {
                    [HealthStatus.Healthy] = StatusCodes.Status200OK,
                    [HealthStatus.Degraded] = StatusCodes.Status200OK,
                    [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
                },
                ResponseWriter = async (context, report) =>
                {
                    context.Response.ContentType = "application/json";
                    string result = JsonSerializer.Serialize(new
                    {
                        status = report.Status.ToString(),
                        totalDurationMs = report.TotalDuration.TotalMilliseconds,
                        entries = report.Entries.ToDictionary(
                            e => e.Key,
                            e => new
                            {
                                status = e.Value.Status.ToString(),
                                description = e.Value.Description,
                                durationMs = e.Value.Duration.TotalMilliseconds
                            })
                    });
                    await context.Response.WriteAsync(result);
                }
            });

            app.MapHealthChecks("/health/live", new HealthCheckOptions
            {
                Predicate = _ => false,
                ResponseWriter = async (context, _) =>
                {
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync("{\"status\":\"Healthy\"}");
                }
            });

            app.MapHealthChecks("/health/ready", new HealthCheckOptions
            {
                Predicate = check => check.Tags.Contains("ready"),
                ResultStatusCodes =
                {
                    [HealthStatus.Healthy] = StatusCodes.Status200OK,
                    [HealthStatus.Degraded] = StatusCodes.Status200OK,
                    [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
                },
                ResponseWriter = async (context, report) =>
                {
                    context.Response.ContentType = "application/json";
                    string result = JsonSerializer.Serialize(new
                    {
                        status = report.Status.ToString(),
                        database = report.Entries.TryGetValue("database", out HealthReportEntry dbEntry) ? dbEntry.Status.ToString() : "Unknown"
                    });
                    await context.Response.WriteAsync(result);
                }
            });

            app.MapControllers();

            Log.Information("Starting FinSightIQ User Service...");
            app.Run();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "FinSightIQ User Service terminated unexpectedly.");
            throw;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}
