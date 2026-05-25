namespace FactFoundry.TelemetryForge.Server.Data.Entities;

/// <summary>
/// A configured downstream event subscriber that receives enriched telemetry events.
/// </summary>
public class Sink
{
    /// <summary>
    /// Unique identifier.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable name for this sink.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The type of sink: LocalDatabase or HttpEndpoint.
    /// </summary>
    public SinkType Type { get; set; }

    /// <summary>
    /// Whether this sink is currently active and receiving events.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Target URL for HTTP sinks (null for local database).
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    /// Optional authorization header value for HTTP sinks (e.g. "Bearer token123").
    /// </summary>
    public string? AuthHeader { get; set; }

    /// <summary>
    /// Optional site ID to scope this sink to a single site (null = all sites).
    /// </summary>
    public string? SiteId { get; set; }

    /// <summary>
    /// When this sink was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// The type of downstream event sink.
/// </summary>
public enum SinkType
{
    /// <summary>Writes to the local database via EF Core.</summary>
    LocalDatabase,

    /// <summary>POSTs enriched payloads to a configured HTTP URL.</summary>
    HttpEndpoint
}
