using System.Text.Json.Serialization;

namespace FactFoundry.TelemetryForge.Server.Models.Events;

/// <summary>
/// An enriched API request event, ready for publishing to sinks.
/// </summary>
public class EnrichedApiEvent
{
    /// <summary>
    /// The registered site (API) that produced this event.
    /// </summary>
    [JsonPropertyName("site_id")]
    public string SiteId { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable name of the registered API.
    /// </summary>
    [JsonPropertyName("site_name")]
    public string SiteName { get; set; } = string.Empty;

    /// <summary>
    /// Low-cardinality route template (e.g. "/license/{id}").
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
    /// Country name resolved from IP geolocation (null until a GeoIP database is configured).
    /// </summary>
    [JsonPropertyName("country")]
    public string? Country { get; set; }

    /// <summary>
    /// ISO 3166-1 alpha-2 country code resolved from IP geolocation (null until a GeoIP database is configured).
    /// </summary>
    [JsonPropertyName("country_code")]
    public string? CountryCode { get; set; }

    /// <summary>
    /// When the request occurred.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>
    /// Consumer-defined business outcome for the request (e.g. "license_valid"), distinct from
    /// the HTTP status code. Null when the caller supplied none.
    /// </summary>
    [JsonPropertyName("outcome")]
    public string? Outcome { get; set; }
}
