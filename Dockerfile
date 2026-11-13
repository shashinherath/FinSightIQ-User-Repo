# ==============================================================================
# FinSightIQ User Service — Multi-stage Containerfile (.NET 8)
# ==============================================================================

# Stage 1: Build & Publish
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project definitions first for optimal layer caching
COPY ["global.json", "./"]
COPY ["nuget.config", "./"]
COPY ["packages/", "./packages/"]
COPY ["Application/FinSightIQ.UserService.Core/FinSightIQ.UserService.Core.csproj", "Application/FinSightIQ.UserService.Core/"]
COPY ["Application/FinSightIQ.UserService.Infrastructure/FinSightIQ.UserService.Infrastructure.csproj", "Application/FinSightIQ.UserService.Infrastructure/"]
COPY ["Application/FinSightIQ.UserService.Api/FinSightIQ.UserService.Api.csproj", "Application/FinSightIQ.UserService.Api/"]

# Restore packages with optional BuildKit secret for GitHub Packages access
RUN --mount=type=secret,id=nuget_token,required=false \
    if [ -f /run/secrets/nuget_token ]; then \
        export NUGET_TOKEN=$(cat /run/secrets/nuget_token); \
    fi && \
    export NUGET_TOKEN="${NUGET_TOKEN:-ghp_dev_token_placeholder}" && \
    dotnet restore "Application/FinSightIQ.UserService.Api/FinSightIQ.UserService.Api.csproj"

# Copy source code and build
COPY ["Application/FinSightIQ.UserService.Core/", "Application/FinSightIQ.UserService.Core/"]
COPY ["Application/FinSightIQ.UserService.Infrastructure/", "Application/FinSightIQ.UserService.Infrastructure/"]
COPY ["Application/FinSightIQ.UserService.Api/", "Application/FinSightIQ.UserService.Api/"]
WORKDIR "/src/Application/FinSightIQ.UserService.Api"

RUN dotnet build "FinSightIQ.UserService.Api.csproj" -c Release --no-restore
RUN dotnet publish "FinSightIQ.UserService.Api.csproj" -c Release --no-build -o /app/publish /p:UseAppHost=false

# Stage 2: Production Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

# Run as built-in non-root app user
USER $APP_UID

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:5101 \
    ASPNETCORE_ENVIRONMENT=Production

EXPOSE 5101
EXPOSE 8080

HEALTHCHECK --interval=10s --timeout=3s --retries=3 --start-period=5s \
    CMD curl -f http://localhost:5101/health/live || curl -f http://localhost:8080/health/live || exit 1

ENTRYPOINT ["dotnet", "FinSightIQ.UserService.Api.dll"]
