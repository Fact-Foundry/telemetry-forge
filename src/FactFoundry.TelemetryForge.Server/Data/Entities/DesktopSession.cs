namespace FactFoundry.TelemetryForge.Server.Data.Entities;

/// <summary>
/// A stored desktop telemetry session event.
/// </summary>
public class DesktopSession
{
    /// <summary>
    /// Auto-generated unique identifier.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// The registered app that produced this session.
    /// </summary>
    public string SiteId { get; set; } = string.Empty;

    /// <summary>
    /// Client-generated UUID identifying this session. Used to group heartbeat payloads.
    /// </summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// Application name reported by the client.
    /// </summary>
    public string AppName { get; set; } = string.Empty;

    /// <summary>
    /// Application version reported by the client.
    /// </summary>
    public string AppVersion { get; set; } = string.Empty;

    /// <summary>
    /// Runtime platform (e.g. "Windows", "macOS", "Linux").
    /// </summary>
    public string Platform { get; set; } = string.Empty;

    /// <summary>
    /// Operating system version.
    /// </summary>
    public string OsVersion { get; set; } = string.Empty;

    /// <summary>
    /// Hashed machine fingerprint.
    /// </summary>
    public string FingerprintHash { get; set; } = string.Empty;

    /// <summary>
    /// Whether this was the first session from this machine.
    /// </summary>
    public bool IsFirstInstall { get; set; }

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
    /// Number of features used during the session.
    /// </summary>
    public int FeatureCount { get; set; }

    /// <summary>
    /// Number of errors captured during the session.
    /// </summary>
    public int ErrorCount { get; set; }

    /// <summary>
    /// Ordered list of features used during the session.
    /// </summary>
    public List<string> FeaturePath { get; set; } = [];

    /// <summary>
    /// Error events captured during the session.
    /// </summary>
    public List<StoredErrorEvent> ErrorEvents { get; set; } = [];

    /// <summary>
    /// When this record was ingested by the server.
    /// </summary>
    public DateTime IngestedAt { get; set; }
}
