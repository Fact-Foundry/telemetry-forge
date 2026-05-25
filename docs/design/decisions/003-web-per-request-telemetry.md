# ADR-003: Per-Request Web Telemetry and Periodic Desktop/Mobile Heartbeats

**Date:** 2026-05-25
**Status:** Proposed

## Decision

Two changes to the telemetry delivery model:

1. **Web — per-request events.** Each HTTP request (or Blazor navigation) produces one telemetry event sent immediately to the server. Session-level aggregates (entry page, exit page, page count, duration) become server-side computations over the raw events. Raw events are stored in a `WebEvents` table; a background job materializes closed sessions into `WebSessions` after an inactivity window expires.

2. **Desktop and mobile — periodic heartbeats.** Instead of accumulating the entire session in memory and flushing once at app close, the client sends a partial session update every 15–30 minutes. The final flush on app close sends any remaining data. This prevents oversized payloads from long-running sessions and gives the server near-real-time visibility into active desktop/mobile users.

## Context

The current spec (Section: "Blazor Server Circuit Tracking") describes accumulating all navigation events in memory during a Blazor Server circuit and flushing the complete session record when the circuit closes. This design has three fundamental problems:

### 1. No real-time visibility

With end-of-session flush, the server has no data until a session ends. You cannot answer "how many people are on the site right now?" or "how many visitors in the last 5 minutes?" — the most basic questions an operator asks when testing or monitoring a live deployment. Data only appears after the visitor leaves, assuming the flush succeeds.

### 2. Unreliable flush

The circuit close event is not guaranteed to fire:

- **Browser tab closed / navigated away** — the server detects disconnection after a timeout (~3 minutes by default), but the circuit handler may not execute cleanly
- **Server process recycle / crash** — all in-memory sessions are lost with no flush
- **Network interruption** — client disconnects, server eventually times out, session data may be partially lost
- **Blazor WASM** — no server-side circuit exists; the WASM app runs entirely in the browser and has no reliable unload event
- **ASP.NET MVC / Razor Pages** — no circuit concept at all; each request is independent

In all of these cases, the session is either lost entirely or missing its final pages.

### 3. Incompatible with non-.NET platforms

The spec states the server should accept telemetry from "any platform" via raw REST calls, and the web package design should be adaptable to other languages. PHP, Python (Django/Flask), Ruby, and Node.js all use a request-response model where each HTTP request is independent — there is no long-lived process to accumulate session state. A PHP process starts, handles one request, and exits. There is nowhere to buffer a session.

End-of-session flush is only viable for Blazor Server circuits and desktop/mobile apps. It is architecturally incompatible with the majority of web frameworks.

## New Design

### Web event payload (sent per request)

```json
{
  "ip_hash":        "string (SHA-256, daily rotating salt)",
  "ga_hash":        "string | null",
  "user_agent":     "string",
  "referrer":       "string | null",
  "language":       "string",
  "page":           "string",
  "status_code":    "integer",
  "event_type":     "string (page_view | custom | link_click | circuit_close)",
  "event_name":     "string | null (only for custom events)",
  "event_data":     "object | null (only for custom events)",
  "target_url":     "string | null (only for link_click events)",
  "timestamp":      "ISO 8601 datetime",
  "dnt":            "boolean"
}
```

The baseline event type is `page_view` — one per HTTP request, fired by the middleware at the end of the request pipeline. The `custom` type is available on all platforms for developer-defined events (see below). The `link_click` and `circuit_close` types are Blazor Server enhancements.

Each event is small and stateless — the middleware does not need to maintain any in-memory state.

### Custom events (all platforms)

Any platform can send a `custom` event to track application-specific interactions. The server exposes a general-purpose event type that developers can fire from anywhere they have server-side code — a form submission handler, a button click in Blazor, a POST route in PHP, a Django view, etc.

```json
{
  "ip_hash":        "string",
  "ga_hash":        "string | null",
  "page":           "string (page where the event occurred)",
  "event_type":     "custom",
  "event_name":     "string (developer-defined, e.g. 'contact_form_submit', 'download_brochure')",
  "event_data":     "object | null (optional key-value pairs)",
  "timestamp":      "ISO 8601 datetime",
  "dnt":            "boolean"
}
```

The .NET middleware provides a service that can be injected and called directly:

```csharp
public class ContactController : Controller
{
    private readonly ITelemetryForge _telemetry;

    public ContactController(ITelemetryForge telemetry) => _telemetry = telemetry;

    [HttpPost]
    public IActionResult Submit(ContactForm form)
    {
        _telemetry.TrackEvent("contact_form_submit", new { subject = form.Subject });
        // ... handle form
    }
}
```

For non-.NET platforms, it's a raw POST to the same `/api/telemetry/web` endpoint with `event_type` set to `custom`. No SDK required — just an HTTP call.

Custom events flow through the same `WebEvents` table and event pipeline as page views. This gives developers a JS-free way to track any server-side interaction on any platform, while keeping the decision about what to track in their hands.

### Server-side session assembly

The server groups events into sessions using the daily-salted `session_hash` (derived from IP) and a configurable inactivity window (default: 30 minutes). Session-level fields are computed:

| Field | Computation |
|---|---|
| `session_start` | Timestamp of first event in group |
| `session_end` | Timestamp of most recent event in group |
| `duration_ms` | `session_end - session_start` |
| `entry_page` | Page from first event |
| `exit_page` | Page from most recent event |
| `page_path` | Ordered list of pages from all events |
| `page_count` | Count of events in group |
| `status_codes` | Aggregated status codes across events |
| `is_first_visit` | `visitor_hashes` lookup on first event |

### Storage model

Store raw events in a `WebEvents` table. A background job materializes closed sessions into `WebSessions` after the inactivity window expires (default: 30 minutes of no new events for a given session hash). This gives both raw event granularity for ad-hoc queries and pre-computed session aggregates for dashboard performance.

The `WebEvents` table retains individual event timestamps, enabling queries like "active visitors in the last 5 minutes" by counting distinct session hashes with recent events.

### Blazor Server enhancements

Blazor Server's persistent circuit enables richer tracking on top of the baseline per-request events. These are opt-in enhancements — the per-request model is the universal baseline that works for all platforms.

#### Circuit close signal

When a Blazor Server circuit disconnects (tab close, navigation away, timeout), the middleware sends a lightweight close signal to the server:

```json
{
  "session_hash":   "string (matches the per-request events)",
  "disconnected_at": "ISO 8601 datetime",
  "event_type":     "circuit_close"
}
```

The server uses this to compute the last page's duration — the time between the final web event and the circuit disconnect. Without this signal (non-Blazor platforms), the last page's duration is unknown, matching the behavior of every other server-side analytics tool.

#### Link click tracking (opt-in)

Blazor Server already requires JavaScript for its SignalR connection — the framework does not function without it. This feature adds a small JS interop module on top of that existing dependency (not a standalone script tag) that listens for click events on anchor elements and reports them back through the circuit.

This captures interactions that don't trigger a page navigation:

- External links that open in a new tab/window
- Download links
- Anchor links within the same page
- mailto/tel links

Each click produces a web event with a distinct event type:

```json
{
  "ip_hash":        "string",
  "ga_hash":        "string | null",
  "page":           "string (current page where click occurred)",
  "target_url":     "string (href of the clicked link)",
  "event_type":     "link_click",
  "timestamp":      "ISO 8601 datetime",
  "dnt":            "boolean"
}
```

This is opt-in via the `WebTelemetryOptions`:

```csharp
builder.Services.AddTelemetryForge(options =>
{
    options.Endpoint = "https://telemetry.yourdomain.com";
    options.ApiKey   = "your-site-api-key";
    options.SiteName = "My Blazor App";
    options.TrackLinkClicks = true;  // default: false
});
```

Link click events flow through the same `WebEvents` table and event pipeline as page view events. Since the data is anonymized (hashed IP, no user identity), click tracking provides aggregate behavioral insight ("40% of visitors on the pricing page click the contact link") without individual user profiling.

This enhancement is only available for Blazor Server because it depends on JS interop that is already a hard requirement of the framework. Non-Blazor platforms that need to track specific interactions should use custom events instead — fired from server-side code, no JS involved.

#### Batching optimization

Blazor Server apps can batch navigation events and flush periodically (e.g., every 30 seconds) or on circuit close, rather than making an HTTP call for every SignalR navigation. This reduces traffic while still providing near-real-time data. The server receives the same per-event payload format; the batching is purely a client-side transport optimization.

### Desktop and mobile — periodic heartbeats

Desktop and mobile apps retain end-of-session flush but add periodic heartbeats to avoid two problems:

1. **Oversized payloads** — a desktop app running for 8+ hours accumulates a large feature path and error list; sending it all at once produces an unnecessarily large payload
2. **No real-time visibility** — without heartbeats, the server has no indication that a desktop/mobile user is currently active

#### Heartbeat design

The client sends a partial session update at a configurable interval (default: 15 minutes). Each heartbeat contains:

```json
{
  "app_id":           "string",
  "app_name":         "string",
  "app_version":      "string",
  "platform":         "string",
  "os_version":       "string",
  "fingerprint_hash": "string",
  "license_jwt":      "string | null",
  "session_id":       "string (client-generated UUID, stable for the session)",
  "sequence":         0,
  "session_start":    "ISO 8601 datetime",
  "current_time":     "ISO 8601 datetime",
  "duration_ms":      "integer (since session_start)",
  "feature_path":     ["string (only new features since last heartbeat)"],
  "error_events":     ["(only new errors since last heartbeat)"]
}
```

- `session_id` — a UUID generated at app startup, used by the server to group heartbeats into a single session
- `sequence` — monotonically increasing counter (0 for first heartbeat, 1 for second, etc.) so the server can detect gaps or reordering
- `feature_path` and `error_events` contain only the delta since the last heartbeat, not the full history

The server appends each heartbeat's data to the session record. On app close, a final heartbeat is sent with the complete remaining delta. If the app crashes before the final flush, the server still has all data up to the last heartbeat — at most 15 minutes of data loss instead of the entire session.

The same heartbeat model applies to both desktop and mobile payloads (with `device_hash` / `device_hash_type` replacing `fingerprint_hash` for mobile).

## Consequences

- **Real-time visibility** — the server sees web page visits within seconds and desktop/mobile activity within 15 minutes, enabling "active users right now" queries across all platforms
- **Platform independence** — any language/framework that can make an HTTP POST per request can integrate (PHP, Python, Ruby, etc.)
- **Reduced data loss** — web events are persisted immediately; desktop/mobile lose at most one heartbeat interval (~15 minutes) on crash instead of the entire session
- **More HTTP traffic** — web sends one request per page view instead of one per session; desktop/mobile add one request per heartbeat interval. Mitigated by batching for Blazor Server and by the small payload sizes
- **Server-side session logic** — session grouping, inactivity timeout, and heartbeat assembly move to the server, adding complexity
- **Schema change** — new `WebEvents` table; `WebPayload` DTO reworked to per-event; desktop/mobile payloads gain `session_id` and `sequence` fields; server needs heartbeat assembly logic for desktop/mobile

## Resolved Questions

1. **Inactivity window** — configurable in the admin UI. Default: 30 minutes (GA standard).

2. **Blazor batch interval** — configurable in the client middleware via `WebTelemetryOptions`. Default: 30 seconds. This is a client-side concern — different apps may have different traffic profiles.

    ```csharp
    options.BatchIntervalSeconds = 30;  // default: 30
    ```

3. **Heartbeat interval** — configurable in the client middleware via `DesktopTelemetryOptions` / `MobileTelemetryOptions`. Default: 15 minutes.

    ```csharp
    options.HeartbeatIntervalMinutes = 15;  // default: 15
    ```

4. **Rate limiting** — the current implementation partitions by API key, meaning all visitors to a site share one bucket. With per-request events, this is the wrong granularity — legitimate traffic from many users would hit the limit, while a single abusive client could consume the entire quota.

    The partition key should be **API key + client IP** (from `X-Forwarded-For` or `RemoteIpAddress`), so each visitor gets their own rate limit bucket per site. This allows a low per-visitor limit (e.g., 30/min — no real user hits 30 pages in a minute) that effectively throttles bots and abuse without affecting legitimate multi-user traffic. The limit should be configurable in the admin UI.
