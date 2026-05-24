# TelemetryForge Server

Privacy-first, server-side telemetry for .NET applications. Your data, your infrastructure, forged into insights.

TelemetryForge Server is a self-hosted ASP.NET application that receives telemetry from your web and desktop applications, enriches the data, and forwards it to the downstream sinks of your choice.

## Features

- **Built-in Blazor admin UI** — manage sites, API keys, sinks, and settings from your browser
- **Multi-platform ingestion** — accepts telemetry from web, desktop, and mobile .NET applications
- **Privacy by design** — raw IPs are never persisted, identifiers are hashed, no cookies set
- **Composable event pipeline** — forward enriched events to a local database, HTTP endpoints, or any custom sink
- **Flexible database support** — PostgreSQL, MSSQL, or MySQL
- **OIDC authentication** — optional single sign-on with Microsoft Entra ID or any OIDC provider, configured from the admin UI

## How It Works

```
Your Web App     (FactFoundry.TelemetryForge.Web)     ──→
Your Desktop App (FactFoundry.TelemetryForge.Desktop) ──→  TelemetryForge Server  ──→  Your Database
Your Mobile App  (FactFoundry.TelemetryForge.Mobile)  ──→                         ──→  HTTP Endpoint
Any Platform     (raw REST call)                      ──→                         ──→  Any Sink
```

Client packages are available in a separate repository: [telemetry-forge-sdk](https://github.com/FactFoundry/telemetry-forge-sdk)

## Where Does the Data Go?

TelemetryForge Server uses a configurable event pipeline. After receiving and enriching telemetry payloads, it publishes events to one or more **sinks** — all managed from the admin UI:

- **Local Database** — stores enriched events in your PostgreSQL, MSSQL, or MySQL database for direct querying
- **HTTP Endpoint** — forwards enriched events to external services like Microsoft Fabric Eventhouse, Azure Service Bus, or any webhook

Sinks can run simultaneously — store locally for ad-hoc queries while streaming to Fabric Eventhouse for real-time Power BI dashboards. Configure them per-site or globally.

## Getting Started

1. Deploy the server (Docker, bare metal, or cloud)
2. Open the admin UI and complete the first-run wizard
3. Register your first site or app to get an API key
4. Install the appropriate client NuGet package in your application
5. Start receiving telemetry

## Requirements

- .NET 10 runtime
- PostgreSQL, MSSQL, or MySQL

## License

AGPL-3.0 — see [LICENSE](LICENSE) for details.

*A [Fact Foundry](https://fact-foundry.com) product*
