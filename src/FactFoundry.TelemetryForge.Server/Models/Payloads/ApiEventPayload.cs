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
}
