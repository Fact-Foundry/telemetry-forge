namespace FactFoundry.TelemetryForge.Server.Data.Entities;

/// <summary>
/// A stored web telemetry session event.
/// </summary>
public class WebSession
{
    /// <summary>
    /// Auto-generated unique identifier.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// The site that produced this session.
    /// </summary>
    public string SiteId { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable site name at the time of ingestion.
    /// </summary>
    public string SiteName { get; set; } = string.Empty;

    /// <summary>
    /// Browser platform identifier.
    /// </summary>
    public string Platform { get; set; } = string.Empty;

    /// <summary>
    /// When the session started.
    /// </summary>
    public DateTime SessionStart { get; set; }

    /// <summary>
    /// When the session ended.
    /// </summary>
    public DateTime SessionEnd { get; set; }

    /// <summary>
    /// Session duration in milliseconds.
    /// </summary>
    public int DurationMs { get; set; }

    /// <summary>
    /// Daily-salted hash for session deduplication.
    /// </summary>
    public string SessionHash { get; set; } = string.Empty;

    /// <summary>
    /// Whether this was the visitor's first session on this site.
    /// </summary>
    public bool IsFirstVisit { get; set; }

    /// <summary>
    /// Country resolved from IP geolocation (null until geolocation is implemented).
    /// </summary>
    public string? Country { get; set; }

    /// <summary>
    /// Region resolved from IP geolocation (null until geolocation is implemented).
    /// </summary>
    public string? Region { get; set; }

    /// <summary>
    /// Browser name parsed from User-Agent (null until UA parsing is implemented).
    /// </summary>
    public string? Browser { get; set; }

    /// <summary>
    /// Operating system parsed from User-Agent (null until UA parsing is implemented).
    /// </summary>
    public string? Os { get; set; }

    /// <summary>
    /// Device type parsed from User-Agent (null until UA parsing is implemented).
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
    /// First page visited in the session.
    /// </summary>
    public string EntryPage { get; set; } = string.Empty;

    /// <summary>
    /// Last page visited in the session.
    /// </summary>
    public string ExitPage { get; set; } = string.Empty;

    /// <summary>
    /// Number of pages visited during the session.
    /// </summary>
    public int PageCount { get; set; }

    /// <summary>
    /// Ordered list of pages visited during the session.
    /// </summary>
    public List<string> PagePath { get; set; } = [];

    /// <summary>
    /// HTTP status codes encountered during the session, keyed by status code.
    /// </summary>
    public Dictionary<string, int> StatusCodes { get; set; } = [];

    /// <summary>
    /// When this record was ingested by the server.
    /// </summary>
    public DateTime IngestedAt { get; set; }
}
