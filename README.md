# FinSightIQ User Service

The identity, authentication, session management, user administration, and personal watchlist microservice for the **FinSightIQ** AI-powered stock prediction and financial intelligence platform.

Built on **ASP.NET Core (.NET 8)**, **Clean Architecture**, **PostgreSQL**, **Dapper**, **Flyway**, and **RS256 JWT Authentication**.

---

## 1. Overview & Service Boundaries

The User Service is the authoritative identity and personal state service in the FinSightIQ microservices ecosystem. It owns:

- **User Accounts & Credentials:** User registration, password validation, and BCrypt hashing (cost factor 12).
- **Authentication & RS256 Tokens:** Issuing asymmetric RS256 JWT access tokens containing user claims (`sub`, `email`, `role`, `jti`).
- **Cryptographic Refresh Token Lifecycle:** Generation of 64-byte CSPRNG refresh tokens, SHA-256 database hashing, automatic token rotation, and token-family replay detection.
- **Account State & Security:** Account suspension, soft deletion, and immediate session invalidation on password updates.
- **User Administration:** Role-based administrative user listing, pagination, and user status management (`Viewer`, `Analyst`, `Administrator`).
- **Personal Watchlists:** Symbol normalization (uppercase, trimmed, alphanumeric check) and deduplicated personal stock watchlist storage with strict ownership verification.

```
+-------------------------------------------------------------------------+
|                              React Frontend                             |
+-------------------------------------------------------------------------+
                                     |
                               HTTP (5000)
                                     v
+-------------------------------------------------------------------------+
|                       FinSightIQ API Gateway                            |
|       (Perimeter routing, rate limiting, RS256 public key validation)   |
+-------------------------------------------------------------------------+
                                     |
                       HTTP / Reverse Proxy (5101)
                                     v
+-------------------------------------------------------------------------+
|                       FinSightIQ User Service                           |
|  +--------------------+---------------------+------------------------+  |
|  | Auth & Credentials |  Token Lifecycle    | User & Admin Mgmt      |  |
|  | (BCrypt cost 12)   |  (RS256 + Rotation) | (RBAC & Suspensions)   |  |
|  +--------------------+---------------------+------------------------+  |
|  | Personal Watchlists| RFC 7807 Errors     | Health & Readiness     |  |
|  +--------------------+---------------------+------------------------+  |
+-------------------------------------------------------------------------+
                                     |
                              Npgsql + Dapper
                                     v
+-------------------------------------------------------------------------+
|                  PostgreSQL 15 (`usersdb` schema)                       |
|   - `users` (UUID, email, password_hash, role, status, timestamps)      |
|   - `refresh_tokens` (UUID, user_id, token_hash, family, expiry)        |
|   - `watchlists` (UUID, user_id, symbol, created_at)                    |
+-------------------------------------------------------------------------+
```

---

## 2. Technology Stack

- **Framework:** .NET 8 / ASP.NET Core (C# 12)
- **Architecture:** Clean Architecture (`Api` → `Infrastructure` → `Core`)
- **Database Engine:** PostgreSQL 15 (`usersdb`)
- **Data Access:** Dapper (Micro-ORM) with parameterized SQL & Npgsql connection pooling
- **Schema Migrations:** Flyway (`V1__init.sql`)
- **Password Security:** BCrypt.Net-Next (Work factor 12)
- **JWT / Cryptography:** RS256 (RSA 2048-bit with SHA-256) asymmetric signing
- **Validation:** FluentValidation
- **Logging & Diagnostics:** Microsoft.Extensions.Logging with structured correlation tracking (`X-Correlation-Id`)
- **Testing:** xUnit, FluentAssertions, Moq, Testcontainers (real PostgreSQL 15 containers)
- **Containerization:** Docker multi-stage containerfile, Docker Compose

---

## 3. Repository Structure

```
FinSightIQ-User-Repo/
├── .env.example
├── .gitignore
├── .dockerignore
├── .editorconfig
├── Dockerfile
├── global.json
├── nuget.config
├── Database/                            # Database schema scripts
│   ├── Table/                           # Table definitions (users, refresh_tokens, watchlists)
│   └── Procedures/                      # Stored procedures & database functions
├── packages/                            # FinSightIQ common shared libraries
│   ├── FinSightIQ.BuildingBlocks.0.1.0.nupkg
│   └── FinSightIQ.Contracts.0.1.0.nupkg
├── Application/                         # Solution & projects: Core, Infrastructure, Api, Tests
│   ├── FinSightIQ.UserService.sln
│   ├── FinSightIQ.UserService.Core/     # Domain layer: DTOs, Interfaces, Validators, Services (DTO-only pattern)
│   │   ├── DTOs/
│   │   ├── Interfaces/
│   │   ├── Services/
│   │   └── Validators/
│   ├── FinSightIQ.UserService.Infrastructure/ # Persistence, Repositories, RS256 Key & Token Management
│   │   ├── Health/
│   │   ├── Persistence/
│   │   ├── Repositories/
│   │   └── Security/
│   ├── FinSightIQ.UserService.Api/      # HTTP Presentation layer, Controllers, Middleware
│   │   ├── Controllers/
│   │   ├── Extensions/
│   │   ├── Middleware/
│   │   ├── appsettings.json
│   │   ├── appsettings.Development.json
│   │   └── Program.cs
│   └── FinSightIQ.UserService.Tests/    # Unit, Contract, and PostgreSQL Integration tests
│       ├── Contract/
│       ├── Integration/
│       └── Unit/
```

---

## 4. API Endpoints Catalog

All endpoints adhere to `/api/v1` versioning, JSON camelCase serialization, UTC timestamps, and RFC 7807 Problem Details for errors.

### 4.1 Authentication & Session (`/api/v1/auth`)

| Method | Path | Auth Policy | Description | Success Code |
|---|---|---|---|---|
| `POST` | `/api/v1/auth/register` | Anonymous | Register new user account with default role `Viewer`. | `201 Created` |
| `POST` | `/api/v1/auth/login` | Anonymous | Authenticate credentials; returns access token & sets HttpOnly refresh cookie. | `200 OK` |
| `POST` | `/api/v1/auth/refresh` | Anonymous (Cookie) | Rotates refresh token, detects replay, and returns new access token. | `200 OK` |
| `POST` | `/api/v1/auth/logout` | Anonymous / Cookie | Revokes active refresh token and clears cookie. | `200 OK` |
| `GET` | `/api/v1/auth/me` | Authenticated | Retrieve authenticated user profile and roles. | `200 OK` |
| `POST` | `/api/v1/auth/change-password` | Authenticated | Change user password; revokes all existing refresh token families. | `200 OK` |

### 4.2 Watchlist Operations (`/api/v1/watchlists` and `/api/v1/watchlist`)

| Method | Path | Auth Policy | Description | Success Code |
|---|---|---|---|---|
| `GET` | `/api/v1/watchlist` | Authenticated | Retrieve the authenticated user's stock watchlist items. | `200 OK` |
| `POST` | `/api/v1/watchlist` | Authenticated | Add a stock symbol to user's watchlist (idempotent, uppercase normalized). | `201 Created` |
| `DELETE`| `/api/v1/watchlist/{symbol}`| Authenticated | Remove a stock symbol from user's watchlist. | `204 No Content` |

*Note: Dual routing `/api/v1/watchlist` and `/api/v1/watchlists` is supported for seamless Gateway compatibility.*

### 4.3 User Administration (`/api/v1/users`)

| Method | Path | Auth Policy | Description | Success Code |
|---|---|---|---|---|
| `GET` | `/api/v1/users` | `AdminPolicy` | Paginated list of users (`page`, `pageSize`, `search`). | `200 OK` |
| `GET` | `/api/v1/users/{id}` | `AdminPolicy` | Retrieve details for a specific user by UUID. | `200 OK` |
| `POST` | `/api/v1/users/{id}/suspend`| `AdminPolicy` | Suspend user account and revoke all sessions. | `200 OK` |
| `POST` | `/api/v1/users/{id}/reinstate`| `AdminPolicy` | Reinstate a suspended user account. | `200 OK` |

### 4.4 Health & Observability

| Method | Path | Description |
|---|---|---|
| `GET` | `/health` | Aggregate application and database health status. |
| `GET` | `/health/live` | Process liveness probe (returns 200 if process is up). |
| `GET` | `/health/ready`| Dependency readiness probe (checks PostgreSQL connectivity). |

---

## 5. Security & Cryptographic Architecture

### 5.1 Password Hashing
- Passwords are validated using strict policy rules (minimum 8 characters, at least 1 uppercase letter, 1 lowercase letter, 1 number, and 1 special character).
- Hashed using **BCrypt.Net** with a work/cost factor of **12**.
- Plaintext passwords and password hashes are **never** logged or returned in responses.

### 5.2 Asymmetric RS256 Tokens
- Access tokens are signed using a 2048-bit RSA private key via `RS256` (RSA-SHA256).
- Standard claims: `sub` (user UUID), `email`, `role` (`Viewer`, `Analyst`, `Administrator`), `jti` (unique token ID), `iss`, `aud`, and `exp` (15 minutes).
- The public key is made available for downstream services and API Gateway validation without revealing the private key.
- For local development convenience, if no RSA private key is configured, an ephemeral in-memory 2048-bit RSA keypair is generated automatically with a logged warning.

### 5.3 Refresh Token Rotation & Replay Detection
- Refresh tokens are generated using a 64-byte cryptographically secure pseudorandom number generator (CSPRNG) encoded via Base64Url.
- Before database storage, refresh tokens are hashed using **SHA-256**; raw tokens are never persisted.
- Refresh tokens are transmitted strictly via `HttpOnly`, `SameSite=Strict`, `Secure` (in Production) cookies named `refreshToken`.
- **Token Families:** Each token belongs to a `family_id`. When a token is rotated, a new token is created in the same family and the previous token is marked `is_used = true`.
- **Replay Detection:** If an expired or already-used token is presented, the system detects potential token theft and immediately revokes all tokens within that entire `family_id` with error code `TOKEN_REUSE_DETECTED`.

### 5.4 Error Handling (RFC 7807)
Standardized Problem Details are returned for all error conditions:
```json
{
  "type": "https://datatracker.ietf.org/doc/html/rfc7807",
  "title": "Validation Failed",
  "status": 400,
  "detail": "One or more validation errors occurred.",
  "code": "VALIDATION_FAILED",
  "instance": "/api/v1/auth/register",
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
  "errors": {
    "Email": ["A valid email address is required."]
  }
}
```

Stable application error codes:
- `VALIDATION_FAILED` (400)
- `UNAUTHENTICATED` (401)
- `TOKEN_EXPIRED` (401)
- `TOKEN_REUSE_DETECTED` (401)
- `FORBIDDEN` (403)
- `ACCOUNT_SUSPENDED` (403)
- `RESOURCE_NOT_FOUND` (404)
- `EMAIL_ALREADY_REGISTERED` (409)
- `INTERNAL_ERROR` (500)

---

## 6. Configuration & Environment Variables

Copy `.env.example` to `.env` or provide environment variables:

| Variable | Default (Dev) | Description |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Development` | ASP.NET Core environment (`Development`, `Staging`, `Production`). |
| `ASPNETCORE_URLS` | `http://+:5101` | Bind URL and port. |
| `ConnectionStrings__UserDb`| `Host=localhost;Port=5432;Database=usersdb;Username=postgres;Password=postgres_dev_password` | PostgreSQL connection string. |
| `Jwt__Issuer` | `finsightiq` | JWT token issuer. |
| `Jwt__Audience` | `finsightiq` | JWT token audience. |
| `Jwt__AccessTokenLifetimeMinutes` | `15` | Access token lifespan. |
| `Jwt__RefreshTokenLifetimeDays` | `7` | Refresh token lifespan. |
| `Jwt__PrivateKeyPem` | *empty (ephemeral)* | PEM-encoded RSA private key for RS256 token signing. |
| `Jwt__PublicKeyPem` | *empty (ephemeral)* | PEM-encoded RSA public key for token verification. |
| `BCrypt__WorkFactor` | `12` | BCrypt password hashing cost factor. |

---

## 7. Local Setup & Execution

### 7.1 Prerequisites
- .NET 8.0 SDK (version 8.0.408 or compatible)
- Docker Desktop (for PostgreSQL and containerized execution)
- Flyway CLI (or run Flyway via Docker)

### 7.2 Database Initialization
The database schema DDL is located in `Database/Table`. To initialize the tables in PostgreSQL:

```powershell
# Using psql or containerized PostgreSQL to execute table definitions
Get-ChildItem -Path "Database/Table/*.sql" | ForEach-Object {
    Get-Content $_.FullName | docker exec -i finsightiq-postgres psql -U postgres -d usersdb
}
```

### 7.3 Building and Testing
```powershell
# Restore dependencies
dotnet restore Application/FinSightIQ.UserService.sln

# Build solution
dotnet build Application/FinSightIQ.UserService.sln -c Release

# Execute all Unit, Contract, and Integration tests
dotnet test Application/FinSightIQ.UserService.sln
```

### 7.4 Running the Service
```powershell
cd Application/FinSightIQ.UserService.Api
dotnet run --launch-profile http
```
The service will start on `http://localhost:5101`.
- **Swagger UI:** `http://localhost:5101/swagger`
- **Liveness probe:** `http://localhost:5101/health/live`
- **Readiness probe:** `http://localhost:5101/health/ready`

### 7.5 Running via Docker
```powershell
# Build image
docker build -t finsightiq-user-service:local .

# Run container
docker run -d `
  --name finsightiq-user-service `
  --network finsightiq-network `
  -p 5101:5101 `
  -e ConnectionStrings__UserDb="Host=finsightiq-postgres;Port=5432;Database=usersdb;Username=postgres;Password=postgres_dev_password" `
  finsightiq-user-service:local
```
