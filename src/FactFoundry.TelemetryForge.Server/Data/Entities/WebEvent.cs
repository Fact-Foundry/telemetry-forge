namespace FactFoundry.TelemetryForge.Server.Data.Entities;

/// <summary>
/// A single web telemetry event (page view, custom event, link click, or circuit close).
/// </summary>
public class WebEvent
{
    /// <summary>
    /// Auto-generated unique identifier.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// The site that produced this event.
    /// </summary>
    public string SiteId { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable site name at the time of ingestion.
    /// </summary>
    public string SiteName { get; set; } = string.Empty;

    /// <summary>
    /// Daily-salted hash used to group events into sessions.
    /// </summary>
    public string SessionHash { get; set; } = string.Empty;

    /// <summary>
    /// Whether this event came from a first-time visitor on this site.
    /// </summary>
    public bool IsFirstVisit { get; set; }

    /// <summary>
    /// Page path where this event occurred.
    /// </summary>
    public string Page { get; set; } = string.Empty;

    /// <summary>
    /// HTTP response status code (for page_view events).
    /// </summary>
    public int StatusCode { get; set; }

    /// <summary>
    /// Type of event: page_view, custom, link_click, circuit_open, or circuit_close.
    /// </summary>
    public string EventType { get; set; } = "page_view";

    /// <summary>
    /// Developer-defined event name (only for custom events).
    /// </summary>
    public string? EventName { get; set; }

    /// <summary>
    /// Developer-defined key-value data serialized as JSON (only for custom events).
    /// </summary>
    public string? EventData { get; set; }

    /// <summary>
    /// Href of the clicked link (only for link_click events).
    /// </summary>
    public string? TargetUrl { get; set; }

    /// <summary>
    /// Country resolved from IP geolocation.
    /// </summary>
    public string? Country { get; set; }

    /// <summary>
    /// Region resolved from IP geolocation.
    /// </summary>
    public string? Region { get; set; }

    /// <summary>
    /// Browser name parsed from User-Agent.
    /// </summary>
    public string? Browser { get; set; }

    /// <summary>
    /// Operating system parsed from User-Agent.
    /// </summary>
    public string? Os { get; set; }

    /// <summary>
    /// Device type parsed from User-Agent (desktop, mobile, tablet, bot).
    /// </summary>
    public string? DeviceType { get; set; }

    /// <summary>
    /// HTTP referrer, if any.
    /// </summary>
    public string? Referrer { get; set; }

    /// <summary>
    /// Browser language preference.
    /// </summary>
    public string Language { get; set; } = string.Empty;

    /// <summary>
    /// When this event occurred on the client.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>
    /// When this event was ingested by the server.
    /// </summary>
    public DateTime IngestedAt { get; set; }

    /// <summary>
    /// Whether this event was flagged as suspected bot traffic.
    /// </summary>
    public bool IsBot { get; set; }

    /// <summary>
    /// Whether this event has been materialized into a WebSession.
    /// </summary>
    public bool Materialized { get; set; }
}
