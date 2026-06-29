using System.Text.Json.Serialization;

namespace FactFoundry.TelemetryForge.Server.Models.Payloads;

/// <summary>
/// Incoming telemetry payload for a single API request (FactFoundry.TelemetryForge API channel).
/// Carries auto-captured request health data only — no caller IP, payloads, or PII.
/// </summary>
public class ApiEventPayload
{
    /// <summary>
    /// Low-cardinality route template (e.g. "/license/{id}"), never the raw resolved path.
    /// </summary>
    [JsonPropertyName("route_template")]
    public string RouteTemplate { get; set; } = string.Empty;

    /// <summary>
    /// HTTP method (GET, POST, etc.).
    /// </summary>
    [JsonPropertyName("method")]
    public string Method { get; set; } = string.Empty;

    /// <summary>
    /// HTTP response status code.
    /// </summary>
    [JsonPropertyName("status_code")]
    public int StatusCode { get; set; }

    /// <summary>
    /// Request handling latency in milliseconds.
    /// </summary>
    [JsonPropertyName("latency_ms")]
    public int LatencyMs { get; set; }

    /// <summary>
    /// When the request occurred.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>
    /// Caller's country as an ISO 3166-1 alpha-2 code, resolved by the SDK from a CDN
    /// geolocation header (e.g. CF-IPCountry) on the original inbound request. Null when
    /// the caller isn't behind a CDN; the server then falls back to IP geolocation.
    /// </summary>
    [JsonPropertyName("country")]
    public string? Country { get; set; }

    /// <summary>
    /// Consumer-defined business outcome for the request (e.g. "license_valid"), distinct from
    /// the HTTP status code. Low-cardinality label set by the calling application. Null when none
    /// was supplied. Any client (PHP, Python, etc.) may set this field directly in the JSON body.
    /// </summary>
    [JsonPropertyName("outcome")]
    public string? Outcome { get; set; }
}
