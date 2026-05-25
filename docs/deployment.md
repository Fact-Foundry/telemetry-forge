# Deployment Guide

This guide covers deploying TelemetryForge Server to a production environment.

## Prerequisites

- [.NET 10 runtime](https://dotnet.microsoft.com/download/dotnet/10.0)
- PostgreSQL or MSSQL database server

## Configuration

TelemetryForge uses standard ASP.NET configuration — settings can be provided via `appsettings.json`, environment variables, or any other [configuration provider](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/).

### Required Settings

| Setting | Description | Example |
|---------|-------------|---------|
| `Database:Provider` | Database engine to use | `PostgreSql` or `SqlServer` |
| `ConnectionStrings:TelemetryForge` | Database connection string | See below |
| `ASPNETCORE_ENVIRONMENT` | Set to `Production` for production deployments | `Production` |

#### PostgreSQL connection string

```
Host=db.example.com;Port=5432;Database=telemetryforge;Username=tfuser;Password=your-password
```

#### SQL Server connection string

```
Server=db.example.com;Database=TelemetryForge;User Id=tfuser;Password=your-password;TrustServerCertificate=True
```

### Optional Settings

| Setting | Description | Default |
|---------|-------------|---------|
| `GeoIP:DatabasePath` | Path to a MaxMind GeoLite2 City `.mmdb` file for IP geolocation | None (geolocation disabled) |

OIDC authentication and other runtime settings are configured from the admin UI after deployment.

### Environment Variables

All settings can be provided as environment variables using the standard ASP.NET `__` (double underscore) separator:

```bash
export ASPNETCORE_ENVIRONMENT=Production
export Database__Provider=PostgreSql
export ConnectionStrings__TelemetryForge="Host=db.example.com;Port=5432;Database=telemetryforge;Username=tfuser;Password=your-password"
export GeoIP__DatabasePath=/opt/geoip/GeoLite2-City.mmdb
```

## Database Setup

TelemetryForge does not run migrations automatically in production. Create the database before starting the server — EF Core will create the schema on first startup in development mode, but for production you should generate the schema from the EF Core model:

```bash
# Generate a SQL script from the EF Core model
dotnet ef dbcontext script -p src/FactFoundry.TelemetryForge.Server/FactFoundry.TelemetryForge.Server.csproj
```

Apply the resulting SQL to your database server before starting the application.

## Deployment Options

### Bare Metal / VM

1. Publish the application:

```bash
dotnet publish src/FactFoundry.TelemetryForge.Server/FactFoundry.TelemetryForge.Server.csproj \
  -c Release \
  -o /opt/telemetryforge
```

2. Configure your settings in `/opt/telemetryforge/appsettings.json` or via environment variables.

3. Run the server:

```bash
cd /opt/telemetryforge
./FactFoundry.TelemetryForge.Server
```

For production, run it behind a reverse proxy (see below) and manage the process with systemd or a similar supervisor.

#### systemd Service Example

Create `/etc/systemd/system/telemetryforge.service`:

```ini
[Unit]
Description=TelemetryForge Server
After=network.target

[Service]
Type=exec
WorkingDirectory=/opt/telemetryforge
ExecStart=/opt/telemetryforge/FactFoundry.TelemetryForge.Server
Restart=always
RestartSec=10
User=telemetryforge
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://localhost:5090

[Install]
WantedBy=multi-user.target
```

```bash
sudo systemctl enable --now telemetryforge
```

### Docker

Build a container image using the .NET SDK:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/FactFoundry.TelemetryForge.Server/FactFoundry.TelemetryForge.Server.csproj \
  -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["./FactFoundry.TelemetryForge.Server"]
```

```bash
docker build -t telemetryforge .
docker run -d \
  -p 8080:8080 \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e Database__Provider=PostgreSql \
  -e ConnectionStrings__TelemetryForge="Host=db;Database=telemetryforge;Username=tfuser;Password=secret" \
  telemetryforge
```

## Reverse Proxy

In production, place TelemetryForge behind a reverse proxy that handles TLS termination. The server listens on HTTP by default — the reverse proxy provides HTTPS to external clients.

### Nginx Example

```nginx
server {
    listen 443 ssl;
    server_name telemetry.example.com;

    ssl_certificate     /etc/ssl/certs/telemetry.example.com.pem;
    ssl_certificate_key /etc/ssl/private/telemetry.example.com.key;

    location / {
        proxy_pass http://localhost:5090;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

> **Note:** The `X-Forwarded-For` header is required for IP geolocation to resolve the real client IP rather than the proxy's address.

## First-Run Setup

After starting the server for the first time:

1. Open the admin UI in your browser
2. Complete the setup wizard — create your admin account and set the server name
3. Register your first site or app to get an API key
4. (Optional) Configure GeoIP and OIDC under **Settings**

## GeoIP Setup

IP geolocation is optional. To enable it:

1. Sign up for a free [MaxMind GeoLite2](https://dev.maxmind.com/geoip/geolite2-free-geolocation-data) account
2. Download the **GeoLite2 City** database (`.mmdb` format)
3. Place the file on the server and set the path via `GeoIP:DatabasePath` in config, environment variable, or the admin UI Settings page

A server restart is required after changing the database path. When configured, web telemetry payloads will be enriched with country and region — the raw IP address is never stored.

## Security Notes

- **HTTPS is enforced in production** — authentication cookies are marked `Secure` when `ASPNETCORE_ENVIRONMENT` is set to `Production`
- **Account lockout** — admin accounts lock for 15 minutes after 5 failed login attempts
- **API keys** — generated with `RandomNumberGenerator` and stored as bcrypt hashes; the raw key is shown once at creation and cannot be retrieved
- **IP addresses** — never persisted; geolocation runs at ingestion time then the address is discarded
- **Visitor identifiers** — one-way hashed before storage; cannot be reversed to identify individuals
