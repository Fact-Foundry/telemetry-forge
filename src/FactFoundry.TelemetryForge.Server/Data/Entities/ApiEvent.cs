namespace FactFoundry.TelemetryForge.Server.Data.Entities;

/// <summary>
/// A stored API request telemetry event (per-request, append-only).
/// </summary>
public class ApiEvent
{
    /// <summary>
    /// Auto-generated unique identifier.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// The registered API that produced this event.
    /// </summary>
    public string SiteId { get; set; } = string.Empty;

    /// <summary>
    /// Low-cardinality route template (e.g. "/license/{id}").
    /// </summary>
    public string RouteTemplate { get; set; } = string.Empty;

    /// <summary>
    /// HTTP method (GET, POST, etc.).
    /// </summary>
    public string Method { get; set; } = string.Empty;

    /// <summary>
    /// HTTP response status code.
    /// </summary>
    public int StatusCode { get; set; }

    /// <summary>
    /// Request handling latency in milliseconds.
    /// </summary>
    public int LatencyMs { get; set; }

    /// <summary>
    /// Country name resolved from IP geolocation (null until a GeoIP database is configured).
    /// </summary>
    public string? Country { get; set; }

    /// <summary>
    /// ISO 3166-1 alpha-2 country code resolved from IP geolocation (null until a GeoIP database is configured).
    /// </summary>
    public string? CountryCode { get; set; }

    /// <summary>
    /// When the request occurred (reported by the client).
    /// </summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>
    /// When this record was ingested by the server.
    /// </summary>
    public DateTime IngestedAt { get; set; }

    /// <summary>
    /// Consumer-defined business outcome for the request (e.g. "license_valid"), distinct from
    /// the HTTP status code. Null when the caller supplied none.
    /// </summary>
    public string? Outcome { get; set; }
}
