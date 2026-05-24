# TelemetryForge — Architecture Specification

*A Fact Foundry product*

## Overview

A two-piece system for tracking web, desktop, and mobile application telemetry entirely server-side, with no JavaScript, no cookies set by the library, and no cross-site tracking. Designed as a privacy-first, GDPR-friendly alternative to Google Analytics for .NET applications.

The system ships as three NuGet packages targeting different hosting contexts, all reporting to a shared central server with a built-in Blazor admin UI.

**Package family:**
```
FactFoundry.TelemetryForge.Web
FactFoundry.TelemetryForge.Desktop
FactFoundry.TelemetryForge.Mobile
FactFoundry.TelemetryForge.Server
```

---

## System Architecture

```
Web App A  (FactFoundry.TelemetryForge.Web)     ──→
Web App B  (FactFoundry.TelemetryForge.Web)     ──→
Desktop A  (FactFoundry.TelemetryForge.Desktop) ──→  FactFoundry.TelemetryForge.Server  ──→  Fabric Eventhouse
Desktop B  (FactFoundry.TelemetryForge.Desktop) ──→          ↕                          ──→  Local Database
Mobile A   (FactFoundry.TelemetryForge.Mobile)  ──→    visitor_hashes DB                ──→  Any subscriber
Any Platform (raw REST call)                    ──→
```

The system consists of two independently deployable pieces:

- **Piece 1 — NuGet Packages**: Lightweight, stateless middleware installed in consuming applications. Three variants — web, desktop, and mobile.
- **Piece 2 — FactFoundry.TelemetryForge.Server**: A minimal ASP.NET API with a built-in Blazor admin UI that receives payloads, resolves visitor identity, enriches records, and forwards events downstream.

---

## Package 1 — `FactFoundry.TelemetryForge.Web`

### Use Case

ASP.NET and Blazor web applications. Identity is ephemeral and inferred from request data — there is no persistent machine identifier available server-side in a web context.

### Responsibilities

- Hook into the ASP.NET HTTP pipeline
- Capture session data from `HttpContext`
- Track Blazor Server circuit lifetime for true session boundaries (where applicable)
- Hash IP address and read `_ga` cookie for identity resolution
- POST session payload to `/api/telemetry/web` on the central server
- No database dependency
- No JavaScript emitted to the client

### Consumer Setup

```csharp
builder.Services.AddTelemetryForge(options =>
{
    options.Endpoint = "https://telemetry.yourdomain.com";
    options.ApiKey   = "your-site-api-key";
    options.SiteName = "My Blazor App";
});

app.UseTelemetryForge();
```

### Configuration Options

```csharp
public class WebTelemetryOptions
{
    public string Endpoint    { get; set; }         // URL of TelemetryForge.Server
    public string ApiKey      { get; set; }         // Per-site API key
    public string SiteName    { get; set; }         // Human-readable site identifier
    public bool   RespectDnt  { get; set; } = true; // Honor Do Not Track header
}
```

### Data Captured from HttpContext

| Field | Source | Notes |
|---|---|---|
| User-Agent | Request header | Browser, OS, device type |
| Referrer | `Referer` header | Traffic source |
| Accept-Language | Request header | Locale/language |
| IP (hashed) | `X-Forwarded-For` / connection | Never stored raw |
| `_ga` value (hashed) | Request cookie | Only if present; never stored raw |
| Request path | URL | Page visited |
| Response status | Pipeline | Error tracking |
| Response time | Middleware brackets | Performance |
| DNT flag | `DNT` header | Respected if configured |

### Blazor Server Circuit Tracking

Blazor Server maintains a persistent SignalR connection per user. The library hooks into circuit lifetime events to track the complete session in memory and write it as a single atomic record on circuit close:

- **Circuit open** → session starts, begin recording navigation via `NavigationManager`
- **Navigation events** → append to in-memory session path
- **Circuit close** → session ends, flush complete record to central server

This provides a true session boundary without reconstructing sessions from fragments after the fact. Circuit closes on tab close, navigation away, or inactivity timeout (~3 minutes by default, configurable).

### Web Payload Schema

```json
{
  "site_id":        "string (from API key lookup)",
  "platform":       "string (blazor-server | blazor-wasm | aspnet | other)",
  "session_start":  "ISO 8601 datetime",
  "session_end":    "ISO 8601 datetime",
  "duration_ms":    "integer",
  "ip_hash":        "string (SHA-256, daily rotating salt)",
  "ga_hash":        "string | null (SHA-256, no salt)",
  "user_agent":     "string",
  "referrer":       "string | null",
  "language":       "string",
  "entry_page":     "string",
  "exit_page":      "string",
  "page_path":      ["string"],
  "status_codes":   {"200": 4, "404": 1},
  "dnt":            "boolean"
}
```

### Identity — Web

Web identity is ephemeral and inferred. There is no persistent machine identifier available without client-side code, so identity is approximated through two layers:

**Layer 1 — Long-term (`visitor_hashes` table on central server)**
- SHA-256 hash of IP or `_ga` value, no salt
- Used only to set `is_first_visit` flag
- No behavioral data — purely a lookup key

**Layer 2 — Session (analytics records)**
- IP hashed with a daily rotating salt
- Provides within-session continuity
- Salt discarded at midnight — permanently irreversible
- Returning visitor detection handled by Layer 1, not the salt

#### Returning Visitor Tradeoff

| Approach | Session tracking | Return visitor tracking | GDPR risk |
|---|---|---|---|
| Raw IP stored | ✅ | ✅ | ❌ High |
| Static hash | ✅ | ✅ | ⚠️ Moderate |
| Daily rotating salt | ✅ | ❌ | ✅ Low |
| IP discarded entirely | ❌ | ❌ | ✅ Lowest |

Returning visitor detection relies on the `visitor_hashes` lookup rather than the session salt, giving the `is_first_visit` flag without long-term behavioral tracking.

---

## Package 2 — `FactFoundry.TelemetryForge.Desktop`

### Use Case

Desktop applications built on .NET — MAUI (desktop targets), Photino, WPF, WinForms, or any hosted Blazor desktop context. Identity is persistent and concrete via machine fingerprinting.

### Responsibilities

- Capture machine fingerprint at startup
- Track application session lifetime (start to close)
- Record feature/component navigation within the app
- POST session payload to `/api/telemetry/desktop` on the central server
- Optionally integrate with an existing licensing JWT
- No JavaScript dependency

### Consumer Setup

```csharp
builder.Services.AddTelemetryForge(options =>
{
    options.Endpoint   = "https://telemetry.yourdomain.com";
    options.ApiKey     = "your-app-api-key";
    options.AppName    = "My Desktop App";
    options.AppVersion = Assembly.GetExecutingAssembly()
                                 .GetName().Version?.ToString();
});
```

### Configuration Options

```csharp
public class DesktopTelemetryOptions
{
    public string Endpoint     { get; set; }  // URL of TelemetryForge.Server
    public string ApiKey       { get; set; }  // Per-app API key
    public string AppName      { get; set; }  // Human-readable app identifier
    public string AppVersion   { get; set; }  // Populated automatically or manually
    public string LicenseJwt   { get; set; }  // Optional — existing license JWT
}
```

### Machine Fingerprinting

Platform-specific stable machine identifiers:

| Platform | Source | Stability |
|---|---|---|
| Windows | Registry `MachineGuid` | ✅ Stable |
| Linux | `/etc/machine-id` | ✅ Stable |
| macOS | `IOPlatformUUID` | ✅ Stable |

The raw identifier is hashed (SHA-256) before leaving the machine. The raw value is never transmitted.

### Data Captured

| Field | Source | Notes |
|---|---|---|
| Fingerprint hash | Platform-specific | Stable machine identity |
| Platform | Runtime detection | `windows`, `linux`, `macos` |
| OS version | `Environment.OSVersion` | |
| App version | Assembly version | |
| Session start/end | App lifetime events | |
| Duration | Calculated | |
| Feature path | Navigation tracking | Components/screens visited |
| Error events | Exception handler | Optional |

### Desktop Payload Schema

```json
{
  "app_id":             "string (from API key lookup)",
  "app_name":           "string",
  "app_version":        "string",
  "platform":           "string (windows | linux | macos)",
  "os_version":         "string",
  "fingerprint_hash":   "string (SHA-256 of machine identifier)",
  "license_jwt":        "string | null (optional)",
  "session_start":      "ISO 8601 datetime",
  "session_end":        "ISO 8601 datetime",
  "duration_ms":        "integer",
  "feature_path":       ["string"],
  "error_events":       [{"feature": "string", "message": "string", "timestamp": "ISO 8601"}]
}
```

### Optional Licensing Integration

If the consuming app uses a JWT-based licensing system, passing the JWT allows the central server to correlate analytics sessions with license records:

- Trial vs paid user behavior segmentation
- Version adoption across license tiers
- Churn signal detection (active license, declining usage)

This is entirely optional — `FactFoundry.TelemetryForge.Desktop` functions fully without a licensing system.

---

## Package 3 — `FactFoundry.TelemetryForge.Mobile`

### Use Case

Mobile applications built on MAUI targeting iOS and Android. Identity is best-effort — mobile platform restrictions make stable fingerprinting less reliable than desktop.

### Responsibilities

- Capture best-available device identifier at startup
- Track application session lifetime
- Record feature/screen navigation
- POST session payload to `/api/telemetry/mobile` on the central server
- Respect platform privacy settings (iOS App Tracking Transparency, Android permissions)

### Consumer Setup

```csharp
builder.Services.AddTelemetryForge(options =>
{
    options.Endpoint   = "https://telemetry.yourdomain.com";
    options.ApiKey     = "your-mobile-api-key";
    options.AppName    = "My Mobile App";
    options.AppVersion = AppInfo.VersionString;
});
```

### Mobile Device Identification

| Platform | Source | Stability |
|---|---|---|
| iOS | `UIDevice.identifierForVendor` | ⚠️ Resets on app reinstall |
| Android | `Settings.Secure.ANDROID_ID` | ⚠️ Resets on factory reset; per-app on Android 8+ |

Because mobile identifiers are less stable, `is_first_install` is best-effort rather than guaranteed. The library generates a fallback GUID stored in app-local storage if the platform identifier is unavailable.

### App Store Compliance

- iOS: Does not use `advertisingIdentifier` (IDFA) — no ATT prompt required
- Android: Does not request `AD_ID` permission
- Collects only operational telemetry — no advertising data

### Mobile Payload Schema

```json
{
  "app_id":           "string (from API key lookup)",
  "app_name":         "string",
  "app_version":      "string",
  "platform":         "string (ios | android)",
  "os_version":       "string",
  "device_hash":      "string (SHA-256 of best-available identifier)",
  "device_hash_type": "string (vendor-id | android-id | generated-guid)",
  "session_start":    "ISO 8601 datetime",
  "session_end":      "ISO 8601 datetime",
  "duration_ms":      "integer",
  "feature_path":     ["string"],
  "error_events":     [{"feature": "string", "message": "string", "timestamp": "ISO 8601"}]
}
```

---

## FactFoundry.TelemetryForge.Server

### Responsibilities

- Receive payloads from all three packages via separate endpoints
- Authenticate requests via per-site/per-app API keys
- Perform `visitor_hashes` lookup to resolve `is_first_visit` / `is_first_install`
- Enrich records (geolocation from IP, User-Agent parsing, etc.)
- Publish enriched events to configured downstream subscribers
- Maintain the `visitor_hashes` table — the only stateful dependency in the system
- Serve the built-in Blazor admin UI

### Endpoints

| Endpoint | Package | Description |
|---|---|---|
| `POST /api/telemetry/web` | `TelemetryForge.Web` | Web session payloads |
| `POST /api/telemetry/desktop` | `TelemetryForge.Desktop` | Desktop session payloads |
| `POST /api/telemetry/mobile` | `TelemetryForge.Mobile` | Mobile session payloads |
| `POST /api/sites/register` | Admin | Register a new site/app, receive API key |

### Deployment Options

The server endpoint is a configurable URL in each package. All of these are valid:

```csharp
// Same machine
options.Endpoint = "http://localhost:5100";

// Local network
options.Endpoint = "http://192.168.1.50:5100";

// Hosted
options.Endpoint = "https://telemetry.yourdomain.com";

// Docker
options.Endpoint = "http://telemetryforge:5100";
```

### API Security

**Key Generation**
- Generated using `RandomNumberGenerator` — cryptographically secure, never `Random` or GUID
- Formatted with an identifiable prefix:
```
tfrg_live_a3f8c2d1e4b7f9a2c5d8e1f4b7a2c5d8
```
- Raw key shown to the developer exactly once at registration — never retrievable again
- Server immediately bcrypt hashes the key and stores only the hash

**Authentication — API key per site/app**
- Keys are stored as bcrypt hashes in the server database, never plain text
- Key transmitted in request header, never in the URL:

```
X-TelemetryForge-Key: tfrg_live_a3f8c2d1e4b7f9a2c5d8e1f4b7a2c5d8
```

**Rate limiting**
- Per API key limits via ASP.NET built-in rate limiting middleware
- Protects against malicious use and runaway client bugs

**Transport**
- HTTPS enforced on public deployments
- HTTP valid for localhost/LAN only

**Payload validation**
- Required fields enforced
- Values validated within reasonable bounds
- Malformed payloads rejected with `400 Bad Request`

### Site/App Registration Flow

```
1. Developer deploys FactFoundry.TelemetryForge.Server
2. Completes first-run wizard in admin UI
3. Registers a new site or app (name, type: web | desktop | mobile)
4. Server generates unique API key — shown once, copy to clipboard
5. Developer adds key to consuming app config
6. Server validates key and routes to correct handler on every request
```

### Registration Table

| Column | Type | Notes |
|---|---|---|
| `site_id` | string | Generated on registration |
| `name` | string | Human-readable |
| `type` | enum | `web`, `desktop`, `mobile` |
| `api_key_hash` | string | bcrypt hash of issued key |
| `registered_at` | datetime | |

### visitor_hashes Table

The single stateful lookup table. Shared across all payload types.

| Column | Type | Notes |
|---|---|---|
| `hash` | string | SHA-256 of IP, `_ga`, fingerprint, or device ID |
| `hash_type` | enum | `ip`, `ga`, `fingerprint`, `vendor-id`, `android-id`, `generated-guid` |
| `source_type` | enum | `web`, `desktop`, `mobile` |
| `first_seen` | datetime | |
| `site_id` | string | |

No behavioral data — purely an existence check for `is_first_visit` / `is_first_install`.

### IP Processing Pipeline (Web Only)

```
Receive raw IP
      ↓
Geolocate → store country/region only
      ↓
Hash with daily rotating salt → session_hash (stored)
      ↓
Hash without salt → lookup in visitor_hashes → set is_first_visit
      ↓
Discard raw IP — never persisted
```

### Enriched Event Schema (Web)

```json
{
  "site_id":        "string",
  "site_name":      "string",
  "platform":       "string",
  "session_start":  "ISO 8601",
  "session_end":    "ISO 8601",
  "duration_ms":    "integer",
  "session_hash":   "string (daily-salted)",
  "is_first_visit": "boolean",
  "country":        "string",
  "region":         "string",
  "browser":        "string (parsed from User-Agent)",
  "os":             "string (parsed from User-Agent)",
  "device_type":    "string (desktop | mobile | tablet | bot)",
  "referrer":       "string | null",
  "language":       "string",
  "entry_page":     "string",
  "exit_page":      "string",
  "page_path":      ["string"],
  "page_count":     "integer",
  "status_codes":   {"200": 4, "404": 1}
}
```

### Enriched Event Schema (Desktop)

```json
{
  "app_id":             "string",
  "app_name":           "string",
  "app_version":        "string",
  "platform":           "string",
  "os_version":         "string",
  "fingerprint_hash":   "string",
  "is_first_install":   "boolean",
  "license_tier":       "string | null (trial | personal | commercial)",
  "session_start":      "ISO 8601",
  "session_end":        "ISO 8601",
  "duration_ms":        "integer",
  "feature_path":       ["string"],
  "error_events":       [{"feature": "string", "message": "string", "timestamp": "ISO 8601"}]
}
```

### Enriched Event Schema (Mobile)

```json
{
  "app_id":           "string",
  "app_name":         "string",
  "app_version":      "string",
  "platform":         "string",
  "os_version":       "string",
  "device_hash":      "string",
  "device_hash_type": "string",
  "is_first_install": "boolean",
  "session_start":    "ISO 8601",
  "session_end":      "ISO 8601",
  "duration_ms":      "integer",
  "feature_path":     ["string"],
  "error_events":     [{"feature": "string", "message": "string", "timestamp": "ISO 8601"}]
}
```

---

## Admin UI

The admin UI is a Blazor application bundled inside `FactFoundry.TelemetryForge.Server`. No separate frontend deployment required — it serves directly from the same process.

### Design Philosophy

- Zero-friction setup — first run to first payload in under 5 minutes
- No config files to hand-edit before the UI is accessible
- No manual database migrations
- Every action completable in as few steps as possible
- Clean, modern Blazor UI consistent with Fact Foundry tooling

### First-Run Wizard

Shown automatically when no admin account exists. Blocks access to the rest of the UI until complete.

```
Step 1 — Create admin account
         Email + password (or SSO if configured)
         ↓
Step 2 — Server name
         Human-readable name for this TelemetryForge instance
         ↓
Step 3 — Database connection
         PostgreSQL, MSSQL, or MySQL — provider selection and connection string
         ↓
Step 4 — Done
         Dashboard loads, prompt to register first site/app
```

### Pages

#### Dashboard
- Activity summary across all registered sites and apps
- Total sessions today / this week / this month
- Per-site/app session volume sparklines
- Recent errors across all sources
- Last payload received per site (health indicator)

#### Sites & Apps
- List of all registered sites and apps with type badge (web / desktop / mobile)
- Status indicator — active (received payload in last 24h), idle, never received
- **Add New** button — opens registration panel:
  - Name
  - Type (web / desktop / mobile)
  - Generate key → shown once with copy button and clear "save this now" warning
- **Manage** per site:
  - View registration details
  - Revoke key (with confirmation)
  - Regenerate key — revokes old, generates new, shown once
  - Delete site and all associated data

#### Event Stream
- Live or paginated feed of recent enriched events
- Filterable by site, type, date range
- Expandable rows showing full event payload
- Useful for verifying a new integration is working

#### Sinks
- Configure downstream event subscribers
- Built-in sink types:
  - **Local Database** — default, no config needed
  - **HTTP Endpoint** — URL + optional auth header
- Add / remove / enable / disable sinks per site or globally
- Test button — sends a sample payload to verify connectivity

#### Settings
- **Server** — instance name, base URL
- **Retention** — how long to keep session records and visitor hashes
- **Security** — enforce HTTPS, rate limit thresholds
- **Admin Accounts** — add/remove admin users, reset passwords

### UX Principles

- Destructive actions (revoke key, delete site) always require explicit confirmation
- API keys shown with one-click copy and a clear "this will not be shown again" warning
- Empty states are helpful — new installs show a getting-started guide, not a blank page
- Error states are specific — "Last payload received 3 days ago" not just a red dot

---

## Event Pipeline (Pub/Sub)

After enrichment the server publishes a `SessionCompleted` event. Subscribers are completely decoupled — adding a new downstream target requires only writing a new subscriber. The packages and server never change.

### Built-in Subscribers

| Subscriber | Description |
|---|---|
| `LocalDatabaseSink` | Writes to local DB via EF Core |
| `HttpSink` | POSTs enriched payload to a configured URL |

### Example Downstream Targets

```
SessionCompleted ──→ Local SQLite / Postgres
                 ──→ Azure Service Bus
                 ──→ Fabric Eventhouse (KQL, Power BI real-time dashboards)
                 ──→ RabbitMQ
                 ──→ Custom HTTP endpoint
```

---

## GDPR Considerations

### What the System Does Not Do

- Does not set cookies
- Does not emit JavaScript
- Does not store raw IP addresses
- Does not build cross-site profiles
- Does not share data with third parties
- Does not use advertising identifiers (IDFA, GAID)

### Lawful Basis

| Data | Basis | Notes |
|---|---|---|
| Web session analytics | Legitimate interest | Proportionate to purpose |
| Desktop session analytics | Legitimate interest | Proportionate to purpose |
| Mobile session analytics | Legitimate interest | Proportionate to purpose |
| `visitor_hashes` | Legitimate interest | Minimal data, no behavioral content |
| Geolocation (country) | Legitimate interest | Country-level is not personal data |
| Machine fingerprint | Legitimate interest | Scoped to single vendor's system |

### Required Actions for Consumers

- Disclose telemetry collection in privacy policy
- Define and enforce a data retention period (configurable in admin UI)
- Implement a process for subject access requests
- Do not store raw IP addresses or `_ga` values outside this system

### GDPR and the `_ga` Cookie

Reading the `_ga` cookie does not create new GDPR obligations. If the user consented to GA cookies, the cookie is already lawfully present. TelemetryForge reads it transiently for identity resolution and never persists the raw value.

---

## Google Analytics Complementary Use (Web)

TelemetryForge.Web and GA answer different questions and complement each other's blind spots:

| Question | TelemetryForge | Google Analytics |
|---|---|---|
| Users with ad blockers | ✅ | ❌ Missed |
| Server errors before render | ✅ | ❌ Missed |
| Bot traffic | ✅ Visible | ⚠️ Filtered |
| JS disabled users | ✅ | ❌ Missed |
| In-page click/scroll events | ❌ | ✅ |
| Cross-device tracking | ❌ | ✅ |
| Conversion funnels | ❌ | ✅ |

The delta between TelemetryForge's visitor count and GA's count reveals exactly how much traffic GA is missing.

---

## Deployment Scenarios

### Solo Developer (Single Machine)

```
[Web App :5000]     ──→
[Desktop App]       ──→  [TelemetryForge.Server :5100]  ──→  [SQLite file]
```

Zero cloud dependency.

### Small Business (LAN / VPS)

```
[Web App A]    ──→
[Web App B]    ──→  [TelemetryForge.Server]  ──→  [Postgres]
[Desktop App]  ──→
[Mobile App]   ──→
```

### Enterprise

```
[Web App A]    ──→
[Web App B]    ──→  [TelemetryForge.Server]  ──→  [Fabric Eventhouse]  ──→  [Power BI]
[Desktop App]  ──→          ↕
[Mobile App]   ──→    [visitor_hashes DB]
```

---

## Positioning

> *"TelemetryForge — privacy-first, server-side telemetry for .NET. Web, desktop, and mobile. Your data, your infrastructure, forged into insights."*

### Competitive Landscape

| Tool | JS Required | Self-Hosted | Native .NET | Desktop Support | Mobile Support | API-First | Admin UI |
|---|---|---|---|---|---|---|---|
| Google Analytics | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| Plausible | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ | ✅ |
| Matomo | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ | ✅ |
| Umami | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ | ✅ |
| Application Insights | ⚠️ Optional | ❌ | ✅ | ✅ | ✅ | ⚠️ | ✅ |
| **TelemetryForge** | ❌ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |

### Key Differentiators

- Zero JavaScript — works for JS-disabled users and ad-blocked environments
- Native Blazor circuit awareness — true session tracking, not reconstructed from fragments
- First-class desktop support via machine fingerprinting
- Mobile support with app store compliant identity
- API-first central server — any platform can send data
- Privacy by design at the architecture level
- Self-hosted — data never leaves your infrastructure
- Composable event pipeline — wire to any downstream store or service
- Built-in Blazor admin UI — zero-friction setup, no config files required
- Single central server handles web, desktop, and mobile telemetry unified

---

*Specification drafted May 2026 — Fact Foundry LLC*
